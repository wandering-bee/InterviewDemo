using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Sled.Core;

namespace MainProc.Service
{
    public sealed class TcpLinkService : ILinkService
    {
        private SledLinkTcp? _link;     // 网络套接字
        private SledChannel? _chn;      // RPC 通道
        private readonly SemaphoreSlim _gate = new(1, 1);   // 保证连接/断开串行化

        #region State / Event
        public bool IsConnected => _link is { IsConnected: true };
        public event Action? Disconnected;
        public event Action<string>? CallIgnored;     // ← 新增
        #endregion

        #region Connect - 建立 / 复用连接
        public async Task<bool> ConnectAsync(string ip, int port)
        {
            await _gate.WaitAsync();
            try
            {
                if (IsConnected) return true;   // 已连直接复用

                _link = new SledLinkTcp();      // 新建
                await _link.ConnectAsync(ip, port);

                _chn = new SledChannel(_link, new KvAsciiCodec());
                var ok = await _chn.CallAsync(Encoding.ASCII.GetBytes("HELLO SLED-LOCAL-DEV"));
                bool pass = ok.Span.SequenceEqual(Encoding.ASCII.GetBytes("OK"));

                Debug.WriteLine(pass ? "✅ 已通过握手" : "❌ 握手失败");
                if (!pass) await DisconnectAsync();          // 防止残留半开
                return pass;
            }
            finally { _gate.Release(); }
        }
        #endregion

        #region Call - RPC 透传
        public Task<ReadOnlyMemory<byte>> CallAsync(ReadOnlyMemory<byte> payload, int timeoutMs = 3000)
        {
            if (!IsConnected || _chn is null)
            {
                CallIgnored?.Invoke("未连接：本次发送已忽略。");        // 触发回调
                return Task.FromResult<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);
            }
            return _chn.CallAsync(payload, timeoutMs);
        }
        #endregion

        #region Disconnect - 主动断链
        public async Task DisconnectAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (!IsConnected) return;

                try { await _link!.DisposeAsync(); }
                catch (Exception ex) { Debug.WriteLine($"⚠️ Dispose: {ex.Message}"); }

                _chn = null;
                _link = null;
                Debug.WriteLine("🔌 已断开");

                Disconnected?.Invoke();          // 告知外部
            }
            finally { _gate.Release(); }
        }
        #endregion
    }
}
