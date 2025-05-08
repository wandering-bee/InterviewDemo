using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MainProc.Service
{
    /// <summary>
    /// 本地服务器进程管理 + 工作目录自动创建
    /// </summary>
    public interface ILocalServerService
    {
        Task StartAsync(int port);
        Task StopAsync();
        bool IsRunning { get; }
        event EventHandler? Exited;
    }
}
