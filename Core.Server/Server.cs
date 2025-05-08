using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Sled.Server
{
    public class TcpServer : IAsyncDisposable
    {
        private readonly string _ip;
        private readonly int _port;
        private TcpListener? _listener;

        public TcpServer(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }


        // 客户端 ↔ 其专属 CTS，便于逐个取消
        private readonly ConcurrentDictionary<TcpClient, CancellationTokenSource> _clients = new();
        private readonly int _maxConnections = 6;          // 足够冗余即可

        /* ---------- 协议常量 ---------- */

        private static readonly Encoding ShiftJis = Encoding.GetEncoding("shift-jis");
        private const byte CR = 0x0D;
        private const byte LF = 0x0A;

        /* ---------- 预编码指令查表 ---------- */

        // <命令字节> → <回复字节>（回复已包含 CRLF）
        private static readonly Dictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _dict
            = BuildDict();

        private static Dictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> BuildDict()
        {
            ReadOnlyMemory<byte> replyZero = ShiftJis.GetBytes("0\r\n");

            var map = new Dictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(ReadOnlyMemoryComparer.Instance);
            for (int i = 1; i <= 3000; ++i)
            {
                // "RD 3001" … "RD 6000"
                string key = $"RD {3000 + i:D4}";
                map[ShiftJis.GetBytes(key).AsMemory()] = replyZero;
            }

            // 再放一个固定的 "PING" / "PONG" 用于纯延迟
            map[ShiftJis.GetBytes("PING").AsMemory()] = ShiftJis.GetBytes("OK\r\n");
            return map;
        }

        private static readonly ReadOnlyMemory<byte> _defaultReply = ShiftJis.GetBytes("?\r\n");

        /* =======================================================================
         * 公开启动入口 —— 传入 CTS，可按需 Stop
         * =====================================================================*/
        public async Task StartAsync(CancellationToken ct = default)
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Logger.Info($"Listening on {_ip}:{_port}");

            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;                      // Stop 被调用
                }

                if (_clients.Count >= _maxConnections)
                {
                    Logger.Warn("Connection refused: server full.");
                    client.Close();
                    continue;
                }

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                if (_clients.TryAdd(client, linkedCts))
                {
                    Logger.Success($"Client connected. Total: {_clients.Count}");
                    _ = HandleClientAsync(client, linkedCts.Token)
                        .ContinueWith(_ => CleanupClient(client));
                }
            }
        }


        /* =======================================================================
         * 每客户端收发协程（带 HELLO/BYE 安全握手）
         * =====================================================================*/
        private static readonly ReadOnlyMemory<byte> OkBytes = Encoding.ASCII.GetBytes("OK\r\n").ToArray();
        private static readonly ReadOnlyMemory<byte> ErrBytes = Encoding.ASCII.GetBytes("ERR\r\n").ToArray();
        private static readonly ReadOnlyMemory<byte> ByeBytes = Encoding.ASCII.GetBytes("BYE\r\n").ToArray();

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            bool authed = false;

            await using var ns = client.GetStream();
            var reader = PipeReader.Create(ns);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    ReadResult rr = await reader.ReadAsync(ct);
                    ReadOnlySequence<byte> buf = rr.Buffer;

                    while (TryReadFrame(ref buf, out ReadOnlyMemory<byte> cmdRaw))
                    {
                        string text = Encoding.ASCII.GetString(cmdRaw.Span);
                        Logger.Recv($"Received: {text}");

                        /* ----- 认证阶段 ----- */
                        if (!authed)
                        {
                            // 当接收到 HELLO 时，进行认证
                            if (text.StartsWith("HELLO "))
                            {
                                // 通过对比 secret（由客户端传入）进行认证
                                string expectedSecret = "SLED-LOCAL-DEV";  // 这里是客户端与进程相匹配的 secret
                                if (text == $"HELLO {expectedSecret}")
                                {
                                    authed = true;
                                    await ns.WriteAsync(OkBytes, ct).ConfigureAwait(false);
                                    Logger.Send("Replied: OK");
                                }
                                else
                                {
                                    await ns.WriteAsync(ErrBytes, ct).ConfigureAwait(false);
                                    Logger.Send("Replied: ERR");
                                }
                            }
                            continue;
                        }

                        /* ----- 已认证：检查 BYE ----- */
                        if (text.StartsWith("BYE "))
                        {
                            string expectedSecret = "SLED-LOCAL-DEV";  // 继续用 secret 验证
                            if (text == $"BYE {expectedSecret}")
                            {
                                await ns.WriteAsync(ByeBytes, ct).ConfigureAwait(false);
                                Logger.Send("Replied: BYE — closing");
                                return;  // 结束协程
                            }
                        }

                        /* ----- 普通业务 ----- */
                        if (!_dict.TryGetValue(cmdRaw, out var reply))
                            reply = _defaultReply;

                        await ns.WriteAsync(reply, ct).ConfigureAwait(false);
                        Logger.Send($"Replied: {Convert.ToHexString(reply.Span)}");
                    }

                    reader.AdvanceTo(buf.Start, buf.End);

                    if (rr.IsCompleted) break;  // 远端主动关闭
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                Logger.Warn($"Client disconnected: {ex.Message}");
            }
        }





        /* ===================================================================
         *  帧拆分：按 CR 或 CRLF 终止符取一帧
         * =================================================================*/
        private static bool TryReadFrame(ref ReadOnlySequence<byte> seq,
                                         out ReadOnlyMemory<byte> frame)
        {
            var rdr = new SequenceReader<byte>(seq);

            // 1️⃣ 先找 CR（必有）
            if (!rdr.TryReadTo(out ReadOnlySequence<byte> body,
                               (byte)0x0D,                // CR
                               advancePastDelimiter: true))
            {
                frame = default;
                return false;                               // 不完整
            }

            // 2️⃣ 若紧跟着一个 LF，则丢弃
            if (rdr.TryPeek(out byte next) && next == 0x0A) // LF
                rdr.Advance(1);

            // 3️⃣ 输出连续内存，供上层查字典
            frame = body.IsSingleSegment ? body.First : body.ToArray();

            // 4️⃣ 把已消费部分从原序列切掉
            seq = seq.Slice(rdr.Position);
            return true;
        }

        /* =======================================================================
         * 清理客户端
         * =====================================================================*/
        private void CleanupClient(TcpClient client)
        {
            if (_clients.TryRemove(client, out var cts))
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                    client.Close();
                }
                catch { }

                Logger.Warn($"Client removed. Total: {_clients.Count}");
            }
        }

        /* =======================================================================
         * 释放所有资源
         * =====================================================================*/
        public async ValueTask DisposeAsync()
        {
            _listener?.Stop();

            foreach (var (cli, cts) in _clients)
            {
                cts.Cancel();
                cts.Dispose();
                cli.Close();
            }

            await Task.CompletedTask;
        }

        /* =======================================================================
         * 用作 Dictionary key 的字节序列比较器
         * =====================================================================*/
        private sealed class ReadOnlyMemoryComparer : IEqualityComparer<ReadOnlyMemory<byte>>
        {
            public static readonly ReadOnlyMemoryComparer Instance = new();
            public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y) =>
                x.Span.SequenceEqual(y.Span);

            public int GetHashCode(ReadOnlyMemory<byte> obj)
            {
                unchecked
                {
                    int h = 17;
                    foreach (byte b in obj.Span) h = h * 31 + b;
                    return h;
                }
            }
        }
    }
}
