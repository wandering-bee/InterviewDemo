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

    /* -------------------------------------------------------------------- */
    /*  Encode                                                              */
    /* -------------------------------------------------------------------- */

    /// <summary>
    /// 把 <paramref name="src"/> 尾部追加 “CRLF” 后返回新的 <see cref="ReadOnlyMemory{T}"/>。
    /// </summary>
    /// <remarks>
    /// - **无 Pinned**：改为普通 <c>new byte[]</c>；对象会被 Gen0/1 很快回收；<br/>
    /// - 如需再极致，可改为 <c>ArrayPool<byte>.Shared.Rent</c> + 调用方归还，这里先保持签名不变。
    /// </remarks>
    public ReadOnlyMemory<byte> Encode(ReadOnlySpan<byte> src)
    {
        var buf = new byte[src.Length + 2];     // +2 = CR LF
        src.CopyTo(buf);
        buf[^2] = 0x0D;
        buf[^1] = 0x0A;
        return buf;
    }

    /* -------------------------------------------------------------------- */
    /*  TryDecode                                                           */
    /* -------------------------------------------------------------------- */

    /// <summary>
    /// 从 <paramref name="seq"/> 里按 “CRLF” 拆出一帧，零拷贝或一次拷贝返回 <paramref name="payload"/>。
    /// </summary>
    /// <returns>若帧完整返回 <see langword="true"/>，并把 <paramref name="seq"/> 向后推进。</returns>
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
