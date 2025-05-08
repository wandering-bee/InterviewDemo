using System.Text;
using Sled.Server;

internal class Program
{
    public static async Task<int> Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        int port = args.GetOption("--port", 12006);
        string procSecret = args.GetOption("--secret", "SLED-LOCAL-DEV-PROC");

        try
        {
            Logger.Info($"Starting SLED TCP Server on port {port}…");

            await using var server = new TcpServer("0.0.0.0", port);
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            // 主业务 & 控制通道
            var serverTask = server.StartAsync(cts.Token);
            var ctrlTask = ListenStdInAsync(procSecret, async () =>
            {
                Logger.Warn("EXIT command received → shutting down…");
                cts.Cancel();
                await server.DisposeAsync();
            });

            // READY 机器行（父进程握手用）
            Console.Out.WriteLine($"{{\"event\":\"READY\",\"port\":{port}}}");

            await Task.WhenAny(serverTask, ctrlTask);
            Logger.Info("Server terminated gracefully.");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error($"Unhandled exception: {ex.Message}");
            Console.ReadLine();       // 防止闪退
            return 1;
        }
    }

    private static async Task ListenStdInAsync(string secret, Func<Task> onExit)
    {
        string? line;
        while ((line = await Console.In.ReadLineAsync()) != null)
        {
            if (line.Equals($"EXIT {secret}", StringComparison.Ordinal))
            {
                await onExit();
                break;
            }
        }
    }
}
