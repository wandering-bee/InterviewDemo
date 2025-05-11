using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Extend;
using Windows.UI;
using Microsoft.UI.Xaml.Media.Animation;
using System.Diagnostics;
using System.Text;
using System.Buffers;
using SampleData;
using MainProc;
using System.ComponentModel;
using Sled.Core;
using System.IO.Pipes;
using Demo.Showcase.Extend;
using Windows.Graphics;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Demo.Showcase
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            // 去掉标题栏并扩展内容
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(CustomTitleBar);

            const int w = 1280;   // 目标宽度
            const int h = 800;    // 目标高度

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId wid = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWin = AppWindow.GetFromWindowId(wid);

            appWin.Resize(new SizeInt32(w, h));   // 立即调整

            this.RootPanel.DataContext = VM;

            VM.PropertyChanged += OnVmPropertyChanged;
            VM.LogEmitted += OnVmLogEmitted;

            VM.Link.CallIgnored += msg => AddLog($"⚠️ {msg}");
        }

        // 把 VM 暴露成属性，方便在代码-behind 里偶尔直接用
        public ConnectionViewModel VM { get; } = new();

        private SledChannel? channel;     // 通信通道

        private Storyboard? _heartbeat;

        private void OnVmLogEmitted(string line)
        {
            // 确保在 UI 线程更新控件
            DispatcherQueue.TryEnqueue(() =>
            {
                FullLogList.Items.Add(line);
            });
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConnectionViewModel.IsConnected))
            {
                // ① 所有 UI 改动都用 DispatcherQueue，确保在 UI 线程
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (VM.IsConnected)
                    {
                        ConnectionStatusLight.Fill = new SolidColorBrush(Colors.Green);
                        StartBreathingLight();
                    }
                    else
                    {
                        ConnectionStatusLight.Fill = new SolidColorBrush("#8B0000".GetColor());
                        StopBreathingLight();
                    }
                });
            }
        }

        private void StartBreathingLight()
        {
            if (_heartbeat != null)
            {
                _heartbeat.Stop();
            }

            _heartbeat = new Storyboard();

            var ba = new DoubleAnimation
            {
                From = 0.5,
                To = 1,
                Duration = new Duration(TimeSpan.FromSeconds(2)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(ba, ConnectionStatusLight);
            Storyboard.SetTargetProperty(ba, "Opacity");

            _heartbeat.Children.Add(ba);
            _heartbeat.Begin();
        }

        private void StopBreathingLight()
        {
            if (_heartbeat != null)
            {
                _heartbeat.Stop();
                ConnectionStatusLight.Opacity = 1.0;
            }
        }

        private void IpAddressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue) return; // 避免不必要的更新

            TBxAddress.UpdateTbStatus(IpAddressSlider, Color.FromArgb(255, 208, 231, 255));

        }

        private void PortSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue) return; // 避免不必要的更新

            TBxPort.UpdateTbStatus(PortSlider, Color.FromArgb(255, 208, 231, 255));

        }


        private async void SendCommandButton_Click(object sender, RoutedEventArgs e)
        {
            if (channel == null)
            {
                AddLog("未连接到服务器。");
                return;
            }

            try
            {
                string command = CommandBox.Text.Trim();
                var response = await channel.CallAsync(System.Text.Encoding.ASCII.GetBytes(command));

                string decoded = System.Text.Encoding.ASCII.GetString(response.Span);
                ResponseBox.Text = decoded;
                AddLog("已发送指令：" + command);
                AddLog("收到回应：" + decoded);
            }
            catch (Exception ex)
            {
                AddLog("发送失败：" + ex.Message);
            }
        }

        // ⚙️ 方法：RunAxoneButton_Click —— 启动并建立管道（升级版）
        private NamedPipeClientStream? pipe;

        private async void RunAxoneButton_Click(object sender, RoutedEventArgs e)
        {
             string exePath = PathHelper.LocateExe("Core.Axone", "Core.Axone.exe");
            string workDir = Path.GetDirectoryName(exePath)!;
            string pipeName = "AxonePipe_" + Guid.NewGuid().ToString("N");

            try
            {
                // ① 启动进程
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = workDir,
                    Arguments = $"--pipe {pipeName}",
                    UseShellExecute = false
                };
                Process.Start(psi);

                // ② 自旋等待（最多 30 s，每秒一次）
                pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                for (int retry = 0; retry < 30; retry++)
                {
                    try
                    {
                        await pipe.ConnectAsync(1000);   // 单次 1 s
                        if (pipe.IsConnected) break;
                    }
                    catch (TimeoutException)
                    {
                        // 🔄 继续循环
                    }
                }

                if (pipe?.IsConnected == true)
                {
                    AxoneStartResultBox.Text = "✅ 管道连接成功";
                }
                else
                {
                    throw new TimeoutException("WinForms 未建立管道（>30 s）");
                }
            }
            catch (Exception ex)
            {
                AxoneStartResultBox.Text = $"❌ 连接失败: {ex.Message}";
                pipe?.Dispose();
                pipe = null;
            }
        }


        // ⚙️ 发送演示：Ping
        private async void SendPingButton_Click(object sender, RoutedEventArgs e)
        {
            if (pipe is not { IsConnected: true })
            {
                AxoneStartResultBox.Text = "⚠️ 管道未连接";
                return;
            }

            // 指令格式：JSON 串；此处简单示例
            string msg = """{"cmd":"Ping","time":"$now$"}"""
                         .Replace("$now$", DateTime.Now.ToString("O"));

            byte[] buf = Encoding.UTF8.GetBytes(msg + "\n");
            await pipe.WriteAsync(buf, 0, buf.Length);
            await pipe.FlushAsync();

            AxoneStartResultBox.Text = "📦 已发送 Ping";
        }



        // 性能压测按钮
        private async void StartBenchmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!VM.IsConnected)           // ← 改成判断连接状态
            {
                AddLog("⚠️ 当前未连接，无法发送消息。");
                return;                    // 直接退出
            }

            const int N = 10_000;

            BenchmarkProgressBar.Minimum = 0;
            BenchmarkProgressBar.Maximum = N;
            BenchmarkProgressBar.Value = 0;
            StatsLine.Text = "";
            BenchmarkResultBox.Text = "";
            Hist.TargetSamples = N;
            Hist.Update(Array.Empty<double>());

            AddLog($"🚀 Benchmark start — {N:N0} packets");

            var uiProgress = new Progress<StatSnapshot>(snap =>
            {
                StatsLine.Text =
                    $"#{snap.Done,5}  last:{snap.Last,7:0.0} µs  " +
                    $"avg:{snap.Avg,7:0.0} µs  " +
                    $"time:{snap.Elapsed,6:0.0}s";

                BenchmarkProgressBar.Value = snap.Done;

                Hist.Update(snap.LatSlice);
            });

            /* —— 后台跑压测 —— */
            var summary = await Task.Run(() => RunBenchmarkAsync(N, uiProgress));


            Hist.Update(summary.Latencies);

            BenchmarkResultBox.Text =
                $"min={summary.Min:0.0} µs   " +
                $"p50={summary.P50:0.0}   " +
                $"avg={summary.Avg:0.0}   " +
                $"p90={summary.P90:0.0}   " +
                $"p99={summary.P99:0.0}   " +
                $"max={summary.Max:0.0} µs \n\n" +
                $"总耗时: {summary.TotalSec:0.000}s";

            AddLog("✅ Benchmark done.");
        }


        /* -----------------------------------------------------------
         * ② 核心：RunBenchmarkAsync
         * ----------------------------------------------------------- */
        private BenchmarkSummary RunBenchmarkAsync(int N, IProgress<StatSnapshot> progress)
        {
            var lat = new double[N];          // 预分配
            var swCall = new Stopwatch();
            var swTotal = Stopwatch.StartNew();   // 全程计时
            var swSlice = Stopwatch.StartNew();   // 刷新间隔

            var buf = ArrayPool<byte>.Shared.Rent(8);
            double runningAvg = 0;

            for (int i = 0; i < N; i++)
            {
                int len;
                if ((i & 1) == 0)
                {
                    buf[0] = 80; buf[1] = 73; buf[2] = 78; buf[3] = 71; len = 4;
                }
                else
                {
                    len = Encoding.ASCII.GetBytes($"RD {3001 + (i % 3000)}", buf);
                }

                /* 通信 */
                swCall.Restart();
                VM.Link.CallAsync(new ReadOnlyMemory<byte>(buf, 0, len))
                       .GetAwaiter()
                       .GetResult();
                swCall.Stop();

                /* 记录延迟 */
                double us = swCall.ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency;
                lat[i] = us;
                runningAvg += (us - runningAvg) / (i + 1);

                /* —— 100 包或 500 ms 刷新 UI —— */
                if ((i + 1) % 100 == 0 || swSlice.ElapsedMilliseconds >= 500)
                {
                    // 复制已完成部分供 UI 读
                    var slice = new double[i + 1];
                    Array.Copy(lat, slice, i + 1);

                    progress.Report(new StatSnapshot
                    {
                        Done = i + 1,
                        Last = us,
                        Avg = runningAvg,
                        Elapsed = swTotal.Elapsed.TotalSeconds,
                        LatSlice = slice
                    });
                    swSlice.Restart();             // 只重置间隔计时
                }
            }

            ArrayPool<byte>.Shared.Return(buf);

            Array.Sort(lat);
            return new BenchmarkSummary
            {
                Latencies = lat,                   // 终态完整数组
                Min = lat[0],
                P50 = lat[N / 2],
                Avg = runningAvg,
                P90 = lat[(int)(N * 0.9)],
                P99 = lat[(int)(N * 0.99)],
                Max = lat[^1],
                TotalSec = swTotal.Elapsed.TotalSeconds
            };
        }

        private void AddLog(string text)
        {
            // WinUI 3: Window 对象自带 DispatcherQueue
            DispatcherQueue.TryEnqueue(() =>
            {
                FullLogList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {text}");
            });
        }

    }
}
