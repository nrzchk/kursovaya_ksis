using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

namespace BlockBlast
{
    public class Server
    {
        private TcpListener listener;
        private TcpClient client1;
        private TcpClient client2;

        private NetworkStream stream1;
        private NetworkStream stream2;

        private Thread acceptThread;
        private Thread listenThread;
        private string nickname1 = null; // от client1
        private string nickname2 = null; // от client2


        private ConcurrentQueue<(TcpClient, string)> messageQueue = new ConcurrentQueue<(TcpClient, string)>();

        public event Action<string, TcpClient> OnMessageReceived;

        public void Start(int port)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Console.WriteLine("Сервер запущен...");

            acceptThread = new Thread(AcceptClients);
            acceptThread.Start();

            listenThread = new Thread(HandleMessages);
            listenThread.Start();
        }

        private void AcceptClients()
        {
            try
            {
                client1 = listener.AcceptTcpClient();
                Console.WriteLine("Клиент 1 подключён");
                stream1 = client1.GetStream();
                new Thread(() => ListenToClient(client1, stream1)).Start();

                client2 = listener.AcceptTcpClient();
                Console.WriteLine("Клиент 2 подключён");
                stream2 = client2.GetStream();
                new Thread(() => ListenToClient(client2, stream2)).Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при подключении клиентов: " + ex.Message);
            }
        }

        private void ListenToClient(TcpClient client, NetworkStream stream)
        {
            byte[] buffer = new byte[1024];
            while (true)
            {
                try
                {
                    int byteCount = stream.Read(buffer, 0, buffer.Length);
                    if (byteCount == 0)
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    messageQueue.Enqueue((client, message));
                }
                catch
                {
                    Console.WriteLine("Клиент отключён.");
                    break;
                }
            }
        }

        private void HandleMessages()
        {
            while (true)
            {
                while (messageQueue.TryDequeue(out var msg))
                {
                    var (sender, message) = msg;
                    Console.WriteLine($"Получено: {message}");

                    // --- Сохраняем nickname ---
                    if (message.StartsWith("Nickname|"))
                    {
                        var parts = message.Split('|');
                        if (parts.Length >= 3)
                        {
                            string tag = parts[1];
                            string nick = parts[2];

                            if (sender == client1)
                                nickname1 = nick;
                            else if (sender == client2)
                                nickname2 = nick;

                            // Как только оба ника известны — отправим их противоположным игрокам
                            if (!string.IsNullOrEmpty(nickname1) && !string.IsNullOrEmpty(nickname2))
                            {
                                SendMessage(client1, $"Nickname|C|{nickname2}");
                                SendMessage(client2, $"Nickname|H|{nickname1}");
                            }
                        }
                    }

                    OnMessageReceived?.Invoke(message, sender);

                    TcpClient receiver = sender == client1 ? client2 : client1;
                    if (receiver != null && receiver.Connected)
                    {
                        SendMessage(receiver, message);
                    }

                }

                Thread.Sleep(10);
            }
        }

        public void SendMessage(TcpClient client, string message)
        {
            if (client == null || !client.Connected) return;

            byte[] data = Encoding.UTF8.GetBytes(message);
            try
            {
                client.GetStream().Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при отправке: " + ex.Message);
            }
        }

        public void Stop()
        {
            try
            {
                client1?.Close();
                client2?.Close();
                listener?.Stop();  // Завершаем слушатель и освобождаем порт
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при закрытии: {ex.Message}");
            }
        }

    }
}
