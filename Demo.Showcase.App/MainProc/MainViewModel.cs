using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MainProc.Service;
using System.ComponentModel;

namespace MainProc;

/// <summary>
/// ViewModel：UI ⇄ Service
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILinkService _link;
    private readonly ILocalServerService _server;
    private const int DefaultPort = 12006;

    // 把只读字段包一层只读属性即可
    public ILinkService Link => _link;

    [ObservableProperty] private bool isConnected;

    public MainViewModel()
    {
        _link = new TcpLinkService();
        _server = new LocalServerService(@"D:\CodeHub\ViridisNetSolution\Core.Server\bin\Debug\net8.0-windows10.0.19041.0\Core.Server.exe");
        _server.Exited += (_, __) => AddLog("⚠️ 本地服务器退出。");

        // 初始化可编辑字段
        Ip = "127.0.0.1";
        Port = DefaultPort;
    }

    // ---------- 可绑定到 TextBox ----------
    [ObservableProperty] private string ip;
    [ObservableProperty] private int port;

    // ---------- 命令 ----------
    [RelayCommand]
    private async Task ConnectAsync()
    {
        var ok = await _link.ConnectAsync(Ip, Port);
        IsConnected = ok;
        AddLog(ok ? "连接并认证成功。" : "认证失败。");
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
