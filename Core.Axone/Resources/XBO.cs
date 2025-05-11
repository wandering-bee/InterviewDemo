using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Axone.Resources;

/* -------------------------------------------------------
 *  基础 GPUBuffer：持久映射 + Named API
 * ----------------------------------------------------- */
public unsafe class GPUBuffer<T> : IDisposable where T : unmanaged {
    public readonly uint Handle;
    public readonly BufferTarget Target;
    public readonly int Capacity;     // 元素数
    protected readonly IntPtr _mappedPtr;

    protected GPUBuffer(BufferTarget target , int elementCount , BufferStorageFlags flags) {
        Target = target;
        Capacity = elementCount;

        // 创建句柄（OpenTK 4.5 : out uint）
        GL.CreateBuffers(1 , out uint tmp);
        Handle = tmp;

        int bytes = elementCount * Unsafe.SizeOf<T>();
        GL.NamedBufferStorage(Handle , bytes , IntPtr.Zero , flags);

        if ((flags & BufferStorageFlags.MapPersistentBit) != 0) {

            const BufferAccessMask mapAccess = BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit;

            _mappedPtr = GL.MapNamedBufferRange(Handle , IntPtr.Zero , bytes , mapAccess);
            if (_mappedPtr == IntPtr.Zero)
                throw new InvalidOperationException("持久映射失败");
        }
    }

    public static GPUBuffer<T> CreatePersistent(BufferTarget tgt , int elements)
        => new(tgt , elements ,
               BufferStorageFlags.MapWriteBit |
               BufferStorageFlags.MapPersistentBit |
               BufferStorageFlags.MapCoherentBit |
               BufferStorageFlags.DynamicStorageBit);

    /* ---------- 裸指针转 Span ---------- */
    public Span<T> AsSpan(int start , int length) {
        if (_mappedPtr == IntPtr.Zero) throw new InvalidOperationException("未持久映射");
        if (start < 0 || start + length > Capacity) throw new ArgumentOutOfRangeException();
        void* p = (byte*)_mappedPtr + start * Unsafe.SizeOf<T>();
        return new Span<T>(p , length);
    }

    /* ---------- Fence 工具 ---------- */
    protected static IntPtr Fence() => GL.FenceSync(SyncCondition.SyncGpuCommandsComplete , 0);

    protected static bool TryWait(IntPtr sync , bool flushIfBusy) {
        if (sync == IntPtr.Zero) return true;
        const long TIMEOUT_NS = 0;
        var st = GL.ClientWaitSync(sync , ClientWaitSyncFlags.None , TIMEOUT_NS);
        if (st == WaitSyncStatus.AlreadySignaled || st == WaitSyncStatus.ConditionSatisfied) {
            GL.DeleteSync(sync);
            return true;
        }
        if (!flushIfBusy) return false;

#if DEBUG
        Debug.WriteLine($"[GPUStreamBuffer] GPU 拖慢上传帧，等待 flush…");
#endif

        st = GL.ClientWaitSync(sync , ClientWaitSyncFlags.SyncFlushCommandsBit , ulong.MaxValue);
        GL.DeleteSync(sync);
        return st == WaitSyncStatus.ConditionSatisfied || st == WaitSyncStatus.AlreadySignaled;
    }

    public void Dispose() {
        if (_mappedPtr != IntPtr.Zero) GL.UnmapNamedBuffer(Handle);
        GL.DeleteBuffer(Handle);
    }
}

/* -------------------------------------------------------
 *  GPUStreamBuffer：环形流式写入 + 自动 Fence
 * ----------------------------------------------------- */
public unsafe class GPUStreamBuffer<T> : GPUBuffer<T> where T : unmanaged {
    public enum FenceWaitPolicy { WaitIfBusy, SkipIfBusy }

    private int _writePtr;
    private int _lastAllocOffset = -1;
    private readonly object _gate = new();
    private readonly Queue<Region> _live = new();
    private readonly FenceWaitPolicy _policy;
    private int _generation = 0;
    public int Generation => _generation;
    public int FreeElem => Capacity - _writePtr;

    public GPUStreamBuffer(int elements,
                           BufferTarget tgt = BufferTarget.ArrayBuffer,
                           FenceWaitPolicy policy = FenceWaitPolicy.WaitIfBusy)
        : base(tgt, elements, BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.DynamicStorageBit)
    {
        _policy = policy;
    }

    /* ===== 公共 API ===== */

    /// 申请 count 元素写空间，返回 Span，gpuOffset 用 out 返回
    public Span<T> Allocate(int count , out int gpuOffset) {
        lock (_gate) {
            if (!EnsureSpace(count))
                throw new InvalidOperationException("GPUStreamBuffer: 空间不足且策略为 SkipIfBusy");

            gpuOffset = _writePtr;
            _lastAllocOffset = gpuOffset;
            _writePtr += count;
            return AsSpan(gpuOffset , count);
        }
    }

    /// Allocate + 立即 Commit，返回 offset
    public Span<T> AllocateAndCommit(int count, out int gpuOffset)
    {
        // 第一次尝试
        if (!TryAllocate(count, out Span<T> span, out gpuOffset))
        {
            FrameBegin();                 // 强制清理 signaled
            if (!TryAllocate(count, out span, out gpuOffset))
                throw new InvalidOperationException("GPUStreamBuffer: 空间不足");
        }
        CommitLast(count);
        return span;
    }


    /// 对最近一次 Allocate 的区段做 Commit
    public void CommitLast(int count) {
        if (_lastAllocOffset < 0)
            throw new InvalidOperationException("CommitLast: 尚未 Allocate 任何区段");
        lock (_gate) {
            _live.Enqueue(new Region(_lastAllocOffset, count, Fence(), 0));
            _lastAllocOffset = -1;
        }
    }

    /// <summary>每帧开头调用一次；清理 signaled 区段，并返回当前可用容量。</summary>
    public int FrameBegin()
    {
        lock (_gate)
        {
            int freed = 0;
            int count = _live.Count;
            for (int i = 0; i < count; i++)
            {
                Region r = _live.Dequeue();
                if (TryWait(r.Sync, false))
                {
                    freed += r.Len;
                }
                else
                {
                    // 未完成：age+1 后重新入队
                    _live.Enqueue(r with { Age = r.Age + 1 });
                }
            }
            return Capacity - _writePtr + freed;
        }
    }

    /// <summary>硬复位：GPU 确认空闲后清空全部 state。</summary>
    public void Reset()
    {
        lock (_gate)
        {
            while (_live.TryDequeue(out Region r))
                TryWait(r.Sync, true);          // 阻塞等完
            _writePtr = 0;
            _lastAllocOffset = -1;
            _generation++;                      // 代际递增
        }
    }

    /// 申请写入前调用：不足时先 Reset，仍不足则抛异常
    public void EnsureCapacity(int needElem)
    {
        if (needElem <= Capacity)
        {
            if (FreeElem < needElem) Reset();      // 归零后就够用
            return;
        }
        throw new InvalidOperationException(
            $"GPUStreamBuffer<{typeof(T).Name}> 容量不足，需要 {needElem} / 当前 {Capacity}");
    }

    /* ===== 内部 ===== */
    private bool TryAllocate(int count, out Span<T> span, out int off)
    {
        try
        {
            span = Allocate(count, out off);
            return true;
        }
        catch
        {
            span = default;
            off = 0;
            return false;
        }
    }

    private bool EnsureSpace(int count) {
        // (1) wrap 到开头
        if (_writePtr + count > Capacity) _writePtr = 0;

        // (2) 处理可能与 live 区段重叠
        while (_live.TryPeek(out Region r) && Overlap(_writePtr , count , r)) {
            bool done = TryWait(r.Sync , _policy == FenceWaitPolicy.WaitIfBusy);
            if (!done) return false; // SkipIfBusy 且 GPU 仍占用
            _live.Dequeue();
        }
        return true;
    }

    /* ---- GL Fence helpers ---- */
    private static IntPtr Fence()
    {
        return GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
    }

    private static bool TryWait(IntPtr sync, bool shouldBlock)
    {
        const long timeoutNs = 0;               // 立即返回
        const long oneMsNs = 1_000_000;

        if (sync == IntPtr.Zero) return true;

        WaitSyncStatus st = GL.ClientWaitSync(sync,
            shouldBlock ? ClientWaitSyncFlags.None : ClientWaitSyncFlags.SyncFlushCommandsBit,
            shouldBlock ? long.MaxValue : timeoutNs);

        if (st == WaitSyncStatus.AlreadySignaled || st == WaitSyncStatus.ConditionSatisfied)
        {
            GL.DeleteSync(sync);
            return true;
        }

        if (shouldBlock)
        {
            // 简单阻塞式等待
            while (st == WaitSyncStatus.TimeoutExpired)
                st = GL.ClientWaitSync(sync, ClientWaitSyncFlags.None, oneMsNs);

            GL.DeleteSync(sync);
            return true;
        }

        return false;   // SkipIfBusy
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Overlap(int start , int len , Region r) {
        int end = start + len;
        int rEnd = r.Off + r.Len;
        return !(end <= r.Off || start >= rEnd);
    }




    private readonly record struct Region(int Off, int Len, IntPtr Sync , int Age);
}
