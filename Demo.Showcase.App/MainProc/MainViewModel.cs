using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MainProc.Service;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Demo.Showcase.Extend;
using Microsoft.UI.Dispatching;

namespace MainProc;

/// <summary>
/// ViewModel：UI ⇄ Service
/// </summary>
public partial class ConnectionViewModel : ObservableObject
{
    private readonly ILinkService _link;
    private readonly ILocalServerService _server;
    private const int DefaultPort = 12006;

    private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();

    // 把只读字段包一层只读属性即可
    public ILinkService Link => _link;

    [ObservableProperty] private bool isConnected;

    public ConnectionViewModel()
    {
        _link = new TcpLinkService();
        _server = new LocalServerService(PathHelper.LocateExe("Core.Server", "Core.Server.exe"));
        _server.Exited += (_, __) => AddLog("⚠️ 本地服务器退出。");

        // 初始化可编辑字段
        Ip = "127.0.0.1";
        Port = DefaultPort;

        _server.Exited += async (_, __) =>
        {
            _ui.TryEnqueue(() =>
            {
                _ = _link.DisconnectAsync();

                IsConnected = false;      // 现在是在 UI 线程 → 安全
                AddLog("⚠️ 本地服务器退出，连接已关闭。");
            });
        };

        _link.Disconnected += () => _ui.TryEnqueue(() => IsConnected = false);

    }

    // ---------- 可绑定到 TextBox ----------
    [ObservableProperty] private string ip;
    [ObservableProperty] private int port;

    // ---------- 命令 ----------
    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnected)            // 已连 → 断开
        {
            await _link.DisconnectAsync();
            IsConnected = false;
            AddLog("🔌 已断开连接。");
        }
        else                        // 未连 → 连接
        {
            var ok = await _link.ConnectAsync(Ip, Port);
            IsConnected = ok;
            AddLog(ok ? "✅ 连接并认证成功。" : "❌ 认证失败。");
        }
    }


    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task StartServerAsync() => _server.StartAsync(Port);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task StopServerAsync() => _server.StopAsync();


    // 日志 → 方便以后做绑定
    partial void OnLogItemsChanged(IEnumerable<string> value);

    /// <summary>向外部（UI / 调试）发送日志</summary>
    public event Action<string>? LogEmitted;

    private void AddLog(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        LogEmitted?.Invoke(line);       // 关键：抛给外部
    }
}
