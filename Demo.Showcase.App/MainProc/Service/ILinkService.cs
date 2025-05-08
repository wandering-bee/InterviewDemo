using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Sled.Core;

namespace MainProc.Service
{
    /// <summary>
    /// 网络连接服务（示例）
    /// </summary>
    public interface ILinkService
    {
        Task<bool> ConnectAsync(string ip, int port);
        Task<ReadOnlyMemory<byte>> CallAsync(ReadOnlyMemory<byte> payload, int timeoutMs = 3000);
        bool IsConnected { get; }
    }



}