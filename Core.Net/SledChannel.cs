using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Sled.Core;

/// <summary>
/// ISledLink + 编解码 调度器（高吞吐版）<br/>
/// ① 排队 + 流水发送 ② 后台 PipeReader 拆帧 ③ FIFO 配对结果
/// </summary>
public sealed class SledChannel : IAsyncDisposable
{
    /* ────────── 构造注入 ────────── */
    private readonly ISledLink _link;
    private readonly ISledCodec _codec;

    /* ────────── 并发窗口（流水线深度） ────────── */
    private readonly int _maxInFlight;
    private readonly SemaphoreSlim _window;              // 控制 CallAsync 峰值并发

    /* ────────── 请求通道：单写 (Caller) / 单读 (SendLoop) ────────── */
    private readonly Channel<Outbound> _queue =
        Channel.CreateUnbounded<Outbound>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /* ────────── 等待配对的完成源 FIFO ────────── */
    private readonly ConcurrentQueue<Outbound> _pending = new();

    /* ────────── 入口状态查询 ────────── */
    public ISledLink Link => _link;

    /* =======================================================================
     * ctor
     * ==================================================================== */
    public SledChannel(ISledLink link, ISledCodec codec, int maxInFlight = 8)
    {
        _link = link;
        _codec = codec;
        _maxInFlight = Math.Max(1, maxInFlight);
        _window = new SemaphoreSlim(_maxInFlight, _maxInFlight);

        _ = ProcessLoopAsync();          // 后台启动收发循环
    }

    /* =======================================================================
     * Public API : CallAsync
     * ==================================================================== */
    public async Task<ReadOnlyMemory<byte>> CallAsync(ReadOnlyMemory<byte> request,
                                                      int timeoutMs = 1_000)
    {
        await _window.WaitAsync().ConfigureAwait(false);        // ① 占窗口

        /* ② 申请缓冲（Rent）并编码 CRLF */
        int len = request.Length + 2;
        byte[] buf = ArrayPool<byte>.Shared.Rent(len);
        request.Span.CopyTo(buf);
        buf[len - 2] = 0x0D;          // CR
        buf[len - 1] = 0x0A;          // LF

        var tcs = new TaskCompletionSource<ReadOnlyMemory<byte>>(
                      TaskCreationOptions.RunContinuationsAsynchronously);

        /* ③ 任务完成后无论如何都归还信号量 */
        tcs.Task.ContinueWith(_ => _window.Release(),
                              TaskScheduler.Default);

        /* ④ 入发送队列（Buffer+Length） */
        await _queue.Writer.WriteAsync(new Outbound(buf, len, tcs))
                          .ConfigureAwait(false);

        /* ⑤ 超时等待 */
        return timeoutMs > 0
            ? await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs))
            : await tcs.Task;
    }


    /* =======================================================================
     * ProcessLoop : 单线程收 + 发（避免 Stream 多线程写）
     * ==================================================================== */
    private async Task ProcessLoopAsync(CancellationToken ct = default)
    {
        /* ---------- 接收协程 ---------- */
        var pipe = PipeReader.Create(_link.Stream);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                ReadResult rr = await pipe.ReadAsync(ct);
                ReadOnlySequence<byte> buf = rr.Buffer;

                while (_codec.TryDecode(ref buf, out var payload))
                {
                    if (_pending.TryDequeue(out var ob))
                    {
                        ob.Tcs.TrySetResult(payload);
                        ArrayPool<byte>.Shared.Return(ob.Buffer);
                    }
                }

                pipe.AdvanceTo(buf.Start, buf.End);

                if (rr.IsCompleted) break;
            }
        }, ct);

        /* ---------- 发送协程 ---------- */
        while (await _queue.Reader.WaitToReadAsync(ct))
            while (_queue.Reader.TryRead(out var ob))
            {
                _pending.Enqueue(ob);               // 先登记，防“回包过快”

                try
                {
                    await _link.SendAsync(ob.AsMemory(), ct).ConfigureAwait(false);   // ★ 用 AsMemory()
                }
                catch (Exception ex)
                {
                    if (ob.Tcs.TrySetException(ex))
                        ArrayPool<byte>.Shared.Return(ob.Buffer);                     // ★ 归还
                }

            }
    }

    /* =======================================================================
     * Outbound : 请求缓冲 + TaskCompletionSource 组合体
     * ==================================================================== */
    private sealed record Outbound(byte[] Buffer,
                                   int Length,
                                   TaskCompletionSource<ReadOnlyMemory<byte>> Tcs)
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> AsMemory() => new(Buffer, 0, Length);
    }


    /* =======================================================================
     * Dispose
     * ==================================================================== */
    public ValueTask DisposeAsync() => _link.DisposeAsync();
}
