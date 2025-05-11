using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sled.Core;

/// <summary>
/// 面向 Sled 协议的 TCP 链路封装。<br/>
/// 支持 IPv4/IPv6 自适应、可选 Keep-Alive，
/// 并以 <see cref="NetworkStream"/> 暴露底层数据流。
/// </summary>
/// <remarks>
/// - **单职责**：仅管理连接建立、发送/接收与保活；
///   编解码与调度由上层 <c>SledChannel</c> 负责。<br/>
/// - **资源管理**：<see cref="DisposeAsync"/> 不主动关闭 <see cref="Socket"/>，
///   由 <see cref="TcpClient"/> 封装完成。<br/>
/// </remarks>
public sealed class SledLinkTcp : ISledLink
{
    private readonly TcpClient _client = CreateClient();
    private NetworkStream _ns = null!;

    public bool IsConnected => _client.Connected;
    public Stream Stream => _ns;

    /// <summary>按平台能力创建并预调优的 <see cref="TcpClient"/> 实例。</summary>
    /// <remarks>
    /// - 自动选择 IPv4 / IPv6（双模）；<br/>
    /// - 预设收发缓冲区 64 KB、<c>NoDelay=true</c>；<br/>
    /// - 仅用于字段初始化，后续由 <see cref="ConnectAsync"/> 建链。
    /// </remarks>
    private static TcpClient CreateClient()
    {
        // 如果系统支持 IPv6，就选双模；否则回退 IPv4
        var af = Socket.OSSupportsIPv6
                  ? AddressFamily.InterNetworkV6
                  : AddressFamily.InterNetwork;

        var cli = new TcpClient(af)
        {
            ReceiveBufferSize = 64 * 1024,
            SendBufferSize = 64 * 1024,
            NoDelay = true
        };

        // 同一个 socket 同时连 IPv4/IPv6
        if (af == AddressFamily.InterNetworkV6)cli.Client.DualMode = true;

        return cli;
    }

    /// <summary>异步连接远端并初始化 <see cref="NetworkStream"/>。</summary>
    /// <param name="host">域名或 IP，支持 IPv4/IPv6。</param>
    /// <param name="port">端口号。</param>
    /// <param name="ct">取消令牌，提前终止时触发 <see cref="OperationCanceledException"/>。</param>
    /// <remarks>
    /// - 成功后立即开启 TCP Keep-Alive（30 s idle / 5 s interval）。<br/>
    /// - <see cref="NetworkStream"/> 以 <c>ownsSocket=false</c> 创建，释放由 <see cref="DisposeAsync"/> 统一处理。
    /// </remarks>
    /// <exception cref="SocketException">网络不可达、连接被拒等。</exception>
    /// <exception cref="OperationCanceledException">在连接过程中被取消。</exception>
    public async ValueTask ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        await _client.ConnectAsync(host, port, ct).ConfigureAwait(false);

        SetKeepAlive(_client.Client, true, timeMs: 30_000, intervalMs: 5_000);

        // NetworkStream 默认无内部缓冲，ownsSocket=false 由 Link 自己收尾
        _ns = new NetworkStream(_client.Client, ownsSocket: false);
    }

    /// <summary>将 <paramref name="buf"/> 全量写入网络流。</summary>
    /// <param name="buf">待发送的数据。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已发送字节数，恒等于 <c>buf.Length</c>。</returns>
    /// <remarks>底层调用 <see cref="NetworkStream.WriteAsync"/>，异常原样透传。</remarks>
    public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buf, CancellationToken ct = default)
    {
        await _ns.WriteAsync(buf, ct).ConfigureAwait(false);
        return buf.Length;
    }

    /// <summary>非阻塞读取：若无数据立即返回 0。</summary>
    /// <param name="buf">目标缓冲区。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>读取到的字节数，可能为 0。</returns>
    /// <remarks>
    /// 先检查 <see cref="NetworkStream.DataAvailable"/>；无数据时不触发 I/O。<br/>
    /// 在迁移到 <c>PipeReader</c> 新接口前，保留此方法兼容旧调用方。
    /// </remarks>
    public ValueTask<int> RecvAsync(Memory<byte> buf, CancellationToken ct = default)
    {
        if (!_ns.DataAvailable) return ValueTask.FromResult(0);
        return _ns.ReadAsync(buf, ct);
    }

    /// <summary>释放网络流与底层 <see cref="TcpClient"/>（幂等）。</summary>
    /// <remarks>
    /// - 先尝试关闭 <see cref="NetworkStream"/>，忽略已关闭异常；<br/>
    /// - 随后调用 <see cref="TcpClient.Close"/>；<br/>
    /// - 再次调用本方法将立即返回已完成任务。
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        try { _ns?.Dispose(); } catch { }
        _client.Close();
        return ValueTask.CompletedTask;
    }

    /// <summary>按平台差异设置 TCP Keep-Alive 开关与时序。</summary>
    /// <param name="sock">目标 <see cref="Socket"/>。</param>
    /// <param name="on">是否启用。</param>
    /// <param name="timeMs">空闲多少毫秒后首次探测。</param>
    /// <param name="intervalMs">探测间隔毫秒。</param>
    /// <remarks>
    /// - Windows 使用 <see cref="IOControlCode.KeepAliveValues"/> 注入 12 字节结构；<br/>
    /// - Linux 走 <c>setsockopt</c>，魔数 <c>SOL_TCP=6</c>，选项 <c>TCP_KEEPIDLE/INTVL</c>。<br/>
    /// - 仅在 <see cref="ConnectAsync"/> 成功后调用一次。
    /// </remarks>
    private static void SetKeepAlive(Socket sock, bool on, uint timeMs, uint intervalMs)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Span<byte> blob = stackalloc byte[12];
            BitConverter.GetBytes(on ? 1u : 0u).CopyTo(blob[..4]);
            BitConverter.GetBytes(timeMs).CopyTo(blob[4..8]);
            BitConverter.GetBytes(intervalMs).CopyTo(blob[8..12]);

            sock.IOControl(IOControlCode.KeepAliveValues, blob.ToArray(), null);
        }
        else
        {
            const int SOL_TCP = 6;   // platform‐independent magic for Linux
            sock.SetSocketOption(SocketOptionLevel.Socket,
                                 SocketOptionName.KeepAlive, on ? 1 : 0);
            if (on)
            {
                // TCP_KEEPIDLE
                sock.SetSocketOption((SocketOptionLevel)SOL_TCP, (SocketOptionName)0x4,
                                     (int)(timeMs / 1000));
                // TCP_KEEPINTVL
                sock.SetSocketOption((SocketOptionLevel)SOL_TCP, (SocketOptionName)0x5,
                                     (int)(intervalMs / 1000));
            }
        }
    }
}
