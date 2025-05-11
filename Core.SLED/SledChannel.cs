using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Sled.Core;

/// <summary>
/// ISledLink + 编解码 调度器（高吞吐版）。
/// </summary>
/// <remarks>
/// 设计要点：<br/>
/// 1. 发送队列：排队并限流，避免多线程直接写 <see cref="Stream"/>；<br/>
/// 2. 并发窗口：<see cref="_window"/> 控制飞行中请求数；<br/>
/// 3. 双协程：单线程「收 / 发」循环，配合 FIFO 队列 _pending 保证顺序一致性。<br/>
/// </remarks>
public sealed class SledChannel : IAsyncDisposable
{
    // 构造注入
    private readonly ISledLink _link;
    private readonly ISledCodec _codec;

    // 并发窗口（流水线深度）
    private readonly int _maxInFlight;
    // 控制 CallAsync 峰值并发
    private readonly SemaphoreSlim _window;

    // 请求通道：单写 (Caller) / 单读 (SendLoop)
    private readonly Channel<Outbound> _queue =
        Channel.CreateUnbounded<Outbound>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    // 等待配对的完成源 FIFO
    private readonly ConcurrentQueue<Outbound> _pending = new();

    // 入口状态查询
    public ISledLink Link => _link;


    public SledChannel(ISledLink link, ISledCodec codec, int maxInFlight = 8)
    {
        _link = link;
        _codec = codec;
        _maxInFlight = Math.Max(1, maxInFlight);
        _window = new SemaphoreSlim(_maxInFlight, _maxInFlight);

        // 后台启动收发循环
        _ = ProcessLoopAsync();
    }


    /// <summary>发送一条请求并等待对应响应。</summary>
    /// <param name="request">已包含命令本体，不含行结束符。</param>
    /// <param name="timeoutMs">超时毫秒，≤0 表示不限时。</param>
    /// <returns>回包负载（不含 CRLF）。</returns>
    /// <exception cref="TimeoutException">超过 <paramref name="timeoutMs"/> 未收到响应。</exception>
    /// <exception cref="OperationCanceledException">链路被显式取消或关闭。</exception>
    public async Task<ReadOnlyMemory<byte>> CallAsync(ReadOnlyMemory<byte> request,
                                                      int timeoutMs = 1_000)
    {
        // ① 占窗口
        await _window.WaitAsync().ConfigureAwait(false);

        // 申请缓冲（Rent）并编码 CRLF
        int len = request.Length + 2;
        byte[] buf = ArrayPool<byte>.Shared.Rent(len);
        request.Span.CopyTo(buf);
        buf[len - 2] = 0x0D;          // CR
        buf[len - 1] = 0x0A;          // LF

        var tcs = new TaskCompletionSource<ReadOnlyMemory<byte>>(
                      TaskCreationOptions.RunContinuationsAsynchronously);

        // ③ 任务完成后无论如何都归还信号量
        tcs.Task.ContinueWith(_ => _window.Release(),
                              TaskScheduler.Default);

        // ④ 入发送队列（Buffer+Length）
        await _queue.Writer.WriteAsync(new Outbound(buf, len, tcs))
                          .ConfigureAwait(false);

        // ⑤ 超时等待
        return timeoutMs > 0
            ? await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs))
            : await tcs.Task;
    }


    /// <summary>驱动 PipeReader 解包与 FIFO 发送的后台循环。</summary>
    /// <remarks>
    /// 私有方法：由构造后启动的后台任务调用。<br/>
    /// - 单线程写底层流，避免并发冲突；<br/>
    /// - 异常不捕获，直透调度层；<br/>
    /// - 取消或远端关闭即退出循环。
    /// </remarks>
    private async Task ProcessLoopAsync(CancellationToken ct = default)
    {
        // ① 启动接收协程：PipeReader → 拆帧 → 完成 _pending
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
                        // ✅ 归还
                        ArrayPool<byte>.Shared.Return(ob.Buffer);
                    }
                }

                pipe.AdvanceTo(buf.Start, buf.End);

                // ⚠️ 远端关闭
                if (rr.IsCompleted) break;
            }
        }, ct);

        // ② 发送循环：_queue → _link.SendAsync
        while (await _queue.Reader.WaitToReadAsync(ct))
            while (_queue.Reader.TryRead(out var ob))
            {
                // 🧠 先登记 , 防止回包过快
                _pending.Enqueue(ob);

                try
                {
                    // 🚚 发送（使用 AsMemory）
                    await _link.SendAsync(ob.AsMemory(), ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ob.Tcs.TrySetException(ex))
                        // ← 归还
                        ArrayPool<byte>.Shared.Return(ob.Buffer);
                }

            }
    }

    /// <summary>封装一次请求的缓冲区与等待响应的 TaskCompletionSource。</summary>
    /// <remarks>
    /// 内部使用：发送前入 _pending，收到响应后完成 TCS 并归还 Buffer。
    /// </remarks>
    private sealed record Outbound(byte[] Buffer,
                                   int Length,
                                   TaskCompletionSource<ReadOnlyMemory<byte>> Tcs)
    {
        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ReadOnlyMemory<byte> AsMemory() => new(Buffer, 0, Length);
    }


    /// <inheritdoc cref="_link.DisposeAsync"/>
    public ValueTask DisposeAsync() => _link.DisposeAsync();
}
