using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MainProc.Service;

namespace MainProc.Service
{
    public class LocalServerService : ILocalServerService
    {
        private readonly string _exePath;
        private readonly string _workDir;
        private Process? _proc;
        private string? _secret;

        public LocalServerService(string exePath)
        {
            _exePath = exePath;
            _workDir = Path.GetDirectoryName(exePath)!;

            // ★★★ 关键：确保文件夹存在（如果已有则无副作用）
            Directory.CreateDirectory(_workDir);
            // 例如额外准备一个 Logs 子目录
            Directory.CreateDirectory(Path.Combine(_workDir, "Logs"));
        }

        public bool IsRunning => _proc is { HasExited: false };
        public event EventHandler? Exited;

        public async Task StartAsync(int port)
        {
            if (IsRunning) return;

            _secret = Guid.NewGuid().ToString("N");

            var psi = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = $"--port {port} --secret {_secret}",
                WorkingDirectory = _workDir,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                CreateNoWindow = false
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.Exited += (_, __) => Exited?.Invoke(this, EventArgs.Empty);
            _proc.Start();

            await WaitForReadyAsync(_proc);
        }


        #region StopAsync - 让外部拿到 Exited 通知
        public async Task StopAsync()
        {
            if (_proc is not { HasExited: false } || _secret == null) return;

            // ① 发送优雅退出指令
            if (_proc.StandardInput != null)
            {
                await _proc.StandardInput.WriteLineAsync($"EXIT {_secret}");
                await _proc.StandardInput.FlushAsync();
            }

            // ② 等待优雅退出，否则强杀
            var exited = await Task.Run(() => _proc!.WaitForExit(1000));
            if (!exited) _proc!.Kill();

            // ③ 手动抛 Exited（若底层事件还没触发）
            OnProcessExited();
        }
        #endregion

        #region Internals - 统一处理进程退出
        private void HookProcess(Process proc)
        {
            _proc = proc;
            _proc.EnableRaisingEvents = true;
            _proc.Exited += (_, __) => OnProcessExited();
        }

        private void OnProcessExited()
        {
            // 保证只抛一次事件
            if (_proc is not { HasExited: true }) return;

            Exited?.Invoke(this, EventArgs.Empty);     // 通知外部
            _proc.Dispose();
            _proc = null;
            _secret = null;
        }
        #endregion

        private static async Task WaitForReadyAsync(Process proc)
        {
            if (proc.StandardError == null) return;
            string? line;
            while ((line = await proc.StandardError.ReadLineAsync()) != null)
            {
                if (line.Contains("\"event\":\"READY\"", StringComparison.OrdinalIgnoreCase))
                    break;
            }
        }
    }

}
