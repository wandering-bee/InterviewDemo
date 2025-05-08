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

        public async Task StopAsync()
        {
            if (_proc is not { HasExited: false } || _secret == null) return;

            // 1. 通过 stdin 发送退出指令
            if (_proc.StandardInput != null)
            {
                await _proc.StandardInput.WriteLineAsync($"EXIT {_secret}");
                await _proc.StandardInput.FlushAsync();
            }

            // 2. 等待优雅退出，否则强杀
            var exited = await Task.Run(() => _proc.WaitForExit(1000));
            if (!exited) _proc.Kill();
        }

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
