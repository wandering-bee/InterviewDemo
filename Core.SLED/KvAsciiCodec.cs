using System;
using System.Buffers;

namespace Sled.Core;

/// <summary>
/// “键值 ASCII” 协议编解码：<br/>
/// <para>REQ  : <c>"PING\r\n"</c> / <c>"RD 3001\r\n"</c> …</para>
/// <para>RESP : <c>"PONG\r\n"</c> / <c>"123\r\n"</c> …</para>
/// </summary>
public sealed class KvAsciiCodec : ISledCodec
{
    private static readonly ReadOnlyMemory<byte> CRLF = new byte[] { 0x0D, 0x0A };   // "\r\n"

    /// <summary>把 <paramref name="src"/> 追加 "CRLF" 后返回新缓冲区。</summary>
    /// <param name="src">需发送的纯负载，不含行结束符。</param>
    /// <returns>带有 <c>"\r\n"</c> 的新 <see cref="ReadOnlyMemory{T}"/>。</returns>
    /// <exception cref="OutOfMemoryException">
    /// 仅在 <paramref name="src"/> 极大或托管堆内存不足时抛出；
    /// 生产环境几乎不会触及。</exception>
    public ReadOnlyMemory<byte> Encode(ReadOnlySpan<byte> src)
    {
        var buf = new byte[src.Length + 2];     // +2 = CR LF
        src.CopyTo(buf);
        buf[^2] = 0x0D;
        buf[^1] = 0x0A;
        return buf;
    }

    /// <summary>尝试从 <paramref name="seq"/> 拆出一帧 ASCII 文本。</summary>
    /// <param name="seq">接收缓冲区，可能包含多帧或半帧。</param>
    /// <param name="payload">成功时返回去掉结尾 CRLF 的负载。</param>
    /// <returns><see langword="true"/>=拆出完整帧；<see langword="false"/>=需等待更多数据。</returns>
    /// <remarks>
    /// - **零拷贝优先**：若帧在单段内直接返回切片；
    /// - **一次拷贝兜底**：跨段罕见场景调用 <see cref="ReadOnlySequence{T}.ToArray"/>。
    /// </remarks>
    public bool TryDecode(ref ReadOnlySequence<byte> seq, out ReadOnlyMemory<byte> payload)
    {
        payload = default;

        var reader = new SequenceReader<byte>(seq);

        // 找到 “…CR LF” 片段
        if (!reader.TryReadTo(out ReadOnlySequence<byte> body, CRLF.Span, advancePastDelimiter: true))
            return false;                       // 不完整，等待更多数据

        // 保证返回的是“连续段”——上层读取更方便
        payload = body.IsSingleSegment
            ? body.First
            : body.ToArray();                   // 极少数跨段的场景才拷贝一次

        // 从源序列切掉已消费的数据
        seq = seq.Slice(reader.Position);
        return true;
    }
}
