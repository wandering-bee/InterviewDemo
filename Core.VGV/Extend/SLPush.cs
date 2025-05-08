using Serilog;
using Serilog.Core;
using System.Windows.Forms;

namespace SLPush
{
    public static class SL
    {

        /// <summary>
        /// Serilog日志器（普通日志）
        /// </summary>
        private static Logger? Logger;

        /// <summary>
        /// Serilog错误日志器（只记录错误/异常）
        /// </summary>
        private static Logger? ErrorLogger;

        /// <summary>
        /// 用于展示 Log 的 RichTextBox 控件
        /// </summary>
        private static RichTextBox? LogBox;

        /// <summary>
        /// 更新日志的事件
        /// </summary>
        public static event Action<string>? LogUpdated;

        /// <summary>
        /// 保存普通运行日志的文件地址
        /// </summary>
        private static readonly string LogPath = "logs/RunLog.txt";

        /// <summary>
        /// 保存错误日志的文件地址
        /// </summary>
        private static readonly string ErrorLogPath = "logs/ErrorLogger.txt";

        static SL()
        {
            SetLogger();
        }

        /// <summary>
        /// 初始化/重置 Logger 和 ErrorLogger
        /// </summary>
        private static void SetLogger()
        {
            // 配置普通日志 Logger
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()  // 输出到控制台
                .WriteTo.File(
                    LogPath,
                    rollingInterval: RollingInterval.Infinite,
                    fileSizeLimitBytes: 1_000_000,
                    rollOnFileSizeLimit: true)
                .CreateLogger();

            // 配置错误日志 ErrorLogger
            // 可以设定 MinimumLevel 为 Error 或 Debug 视需求而定
            ErrorLogger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.Console()
                .WriteTo.File(
                    ErrorLogPath,
                    rollingInterval: RollingInterval.Infinite,
                    fileSizeLimitBytes: 1_000_000,
                    rollOnFileSizeLimit: true)
                .CreateLogger();
        }

        /// <summary>
        /// 配置新的异步日志器
        /// </summary>
        private static void SetLoggerAA()
        {
            Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.Async(a => a.File(LogPath,
                    rollingInterval: RollingInterval.Infinite,
                    fileSizeLimitBytes: 1_000_000,
                    rollOnFileSizeLimit: true))
                .CreateLogger();

            ErrorLogger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.Console()
                .WriteTo.Async(a => a.File(ErrorLogPath,
                    rollingInterval: RollingInterval.Infinite,
                    fileSizeLimitBytes: 1_000_000,
                    rollOnFileSizeLimit: true))
                .CreateLogger();
        }

        /// <summary>
        /// 切换为异步日志器
        /// </summary>
        public static void SwitchToAA()
        {
            DisposeLogger();
            SetLoggerAA();
        }

        /// <summary>
        /// 切换为普通日志器
        /// </summary>
        public static void SwitchToNormal()
        {
            DisposeLogger();
            SetLogger();
        }

        /// <summary>
        /// 释放现有日志器
        /// </summary>
        private static void DisposeLogger()
        {
            Serilog.Log.CloseAndFlush(); // 确保所有日志写入完成
        }

        /// <summary>
        /// 绑定RichTextBox控件（普通日志输出到UI）
        /// </summary>
        /// <param name="richTextBox">控件实例</param>
        public static void BindControl(RichTextBox richTextBox)
        {
            LogBox = richTextBox;
            LogUpdated -= UpdateLog;
            LogUpdated += UpdateLog;
        }

        /// <summary>
        /// 发送普通Log到日志和UI
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void SendLog(string message)
        {
            Logger?.Information(message);
            LogUpdated?.Invoke(message + Environment.NewLine);
        }

        /// <summary>
        /// 清空原有内容后发送普通Log
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void SendCleanLog(string message)
        {
            ClearLog();
            Logger?.Information(message);
            LogUpdated?.Invoke(message + Environment.NewLine);
        }

        /// <summary>
        /// 清空RichTextBox的内容
        /// </summary>
        private static void ClearLog()
        {
            LogBox?.Invoke((MethodInvoker)delegate {
                LogBox.Clear();
            });
        }

        /// <summary>
        /// 仅记录普通日志到文件/控制台，而不更新UI
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void SequLog(string message)
        {
            Logger?.Information(message);
        }

        /// <summary>
        /// 删除同目录下除当前日志文件外的所有历史日志
        /// </summary>
        public static void CleanOldLogs()
        {
            var logDirectoryPath = Path.GetDirectoryName(LogPath);
            if (logDirectoryPath == null) return;
            var directoryInfo = new DirectoryInfo(logDirectoryPath);

            foreach (var file in directoryInfo.GetFiles("*.txt"))
            {
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                    // 文件正在使用中或其他IO异常，跳过
                }
            }
        }

        /// <summary>
        /// 仅更新UI，处理LogUpdated事件
        /// </summary>
        /// <param name="text">要更新到UI的文本</param>
        private static void UpdateLog(string text)
        {
            LogBox?.Invoke((System.Windows.Forms.MethodInvoker)delegate {
                // 如果内容超过一定阈值，截断前面部分
                if (LogBox.TextLength + text.Length > 5000)
                {
                    LogBox.Select(0, LogBox.TextLength - 2000);
                    LogBox.SelectedText = string.Empty;
                }
                LogBox.AppendText(text);
                LogBox.ScrollToCaret();
            });
        }

        // ========== 新增：错误日志部分 ==========

        /// <summary>
        /// 记录错误信息到 ErrorLogger（不更新 UI）。可附带自定义维度信息。
        /// </summary>
        /// <param name="message">错误描述</param>
        /// <param name="level">错误严重度，如 "Error", "Critical", "Warning" 等</param>
        /// <param name="eventID">可选的事件名或分类</param>
        /// <param name="customData">可选的额外描述</param>
        /// <param name="ex">可选的异常对象</param>
        public static void ReportError(
            string message = "",
            string level = "Error",
            string eventID = "",
            string? customData = null,
            Exception? ex = null
        )
        {
            if (ErrorLogger == null) return;

            // 在此可以结构化日志：包含事件ID、严重度、异常类型、其他数据
            if (ex != null)
            {
                ErrorLogger.Error(ex,
                    "[{Severity}] [Event: {EventName}] [CustomData: {CustomData}] => {Message}",
                    level, eventID, customData, message);
            }
            else
            {
                // 如果没有异常对象，只是简单记录
                ErrorLogger.Error(
                    "[{Severity}] [Event: {EventName}] [CustomData: {CustomData}] => {Message}",
                    level, eventID, customData, message);
            }
        }

    } // End class

} // EndNamespace
