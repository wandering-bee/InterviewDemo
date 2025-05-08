using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.VGV.Extend
{
    class PipeServer : IDisposable
    {
        private readonly NamedPipeServerStream srv;
        private readonly Thread listen;

        public Action<string>? OnMessage;

        public PipeServer(string name)
        {
            srv = new NamedPipeServerStream(name,
                                            PipeDirection.InOut,
                                            1,
                                            PipeTransmissionMode.Message,
                                            PipeOptions.Asynchronous);

            listen = new Thread(ListenLoop) { IsBackground = true };
            listen.Start();
        }

        private async void ListenLoop()
        {
            await srv.WaitForConnectionAsync();

            using var reader = new StreamReader(srv, Encoding.UTF8);
            while (srv.IsConnected)
            {
                string? line = await reader.ReadLineAsync();
                if (line == null) break;

                OnMessage?.Invoke(line);   // 🧠 触发回调
            }
        }

        public void Dispose() => srv.Dispose();
    }

}
