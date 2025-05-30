using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlockBlast
{
    public class Client
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public event Action<string> OnMessageReceived;

        public bool IsConnected => tcpClient?.Connected ?? false;

        public async Task ConnectAsync(string ip, int port)
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(ip, port);
            stream = tcpClient.GetStream();

            _ = Task.Run(() => ListenForMessagesAsync(cancellationTokenSource.Token));
        }

        private async Task ListenForMessagesAsync(CancellationToken token)
        {
            byte[] buffer = new byte[1024];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead == 0)
                    {
                        Disconnect();
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    OnMessageReceived?.Invoke(message);
                    Console.WriteLine("Клиент получил: " + message);
                }
                catch
                {
                    Disconnect();
                    break;
                }
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (stream == null || !stream.CanWrite) return;

            byte[] data = Encoding.UTF8.GetBytes(message);
            try
            {
                await stream.WriteAsync(data, 0, data.Length);
            }
            catch
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            cancellationTokenSource.Cancel();

            try { stream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }

            stream = null;
            tcpClient = null;
        }
    }
}
