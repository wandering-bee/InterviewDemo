using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MainProc.Service;
using Sled.Core;

namespace MainProc.Service
{
    public class TcpLinkService : ILinkService
    {
        private SledLinkTcp? _link;
        private SledChannel? _channel;

        public bool IsConnected => _link is { IsConnected: true };

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            _link ??= new SledLinkTcp();
            await _link.ConnectAsync(ip, port);

            _channel ??= new SledChannel(_link, new KvAsciiCodec());
            var ok = await _channel.CallAsync(Encoding.ASCII.GetBytes("HELLO SLED-LOCAL-DEV"));

            return ok.Span.SequenceEqual(Encoding.ASCII.GetBytes("OK"));
        }

        public Task<ReadOnlyMemory<byte>> CallAsync(ReadOnlyMemory<byte> payload, int timeoutMs = 3000)
            => _channel!.CallAsync(payload, timeoutMs);
    }
}
