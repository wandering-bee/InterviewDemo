using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sled.Core
{
    // ① 基础链接接口 —— TCP/UDP/SHM 任意实现
    public interface ISledLink : IAsyncDisposable {
        ValueTask ConnectAsync(string host , int port , CancellationToken ct = default);
        ValueTask<int> SendAsync(ReadOnlyMemory<byte> buf , CancellationToken ct = default);
        ValueTask<int> RecvAsync(Memory<byte> buf , CancellationToken ct = default);

        Stream Stream { get; }

        bool IsConnected { get; }
    }
}
