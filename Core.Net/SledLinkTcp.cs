using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sled.Core;

public sealed class SledLinkTcp : ISledLink
{
    /*──────────────────── 创建 TcpClient（自动 IPv6 / IPv4） ────────────────────*/
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

        if (af == AddressFamily.InterNetworkV6)
            cli.Client.DualMode = true;        // 同一个 socket 同时连 IPv4/IPv6

        return cli;
    }

    /*──────────────────── 字段 ────────────────────*/
    private readonly TcpClient _client = CreateClient();
    private NetworkStream _ns = null!;

    public bool IsConnected => _client.Connected;
    public Stream Stream => _ns;

    /*──────────────────── Connect ────────────────────*/
    public async ValueTask ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        await _client.ConnectAsync(host, port, ct).ConfigureAwait(false);

        SetKeepAlive(_client.Client, true, timeMs: 30_000, intervalMs: 5_000);

        // NetworkStream 默认无内部缓冲，ownsSocket=false 由 Link 自己收尾
        _ns = new NetworkStream(_client.Client, ownsSocket: false);
    }

    /*──────────────────── Send ────────────────────*/
    public async ValueTask<int> SendAsync(ReadOnlyMemory<byte> buf, CancellationToken ct = default)
    {
        await _ns.WriteAsync(buf, ct).ConfigureAwait(false);
        return buf.Length;
    }

    /*──────────────────── Recv (兼容旧接口) ────────────────────*/
    public ValueTask<int> RecvAsync(Memory<byte> buf, CancellationToken ct = default)
    {
        if (!_ns.DataAvailable) return ValueTask.FromResult(0);
        return _ns.ReadAsync(buf, ct);
    }

    /*──────────────────── Dispose ────────────────────*/
    public ValueTask DisposeAsync()
    {
        try { _ns?.Dispose(); } catch { }
        _client.Close();
        return ValueTask.CompletedTask;
    }

    /*──────────────────── Keep-Alive helper ────────────────────*/
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
                sock.SetSocketOption((SocketOptionLevel)SOL_TCP, (SocketOptionName)0x4,
                                     (int)(timeMs / 1000));      // TCP_KEEPIDLE
                sock.SetSocketOption((SocketOptionLevel)SOL_TCP, (SocketOptionName)0x5,
                                     (int)(intervalMs / 1000));  // TCP_KEEPINTVL
            }
        }
    }
}
