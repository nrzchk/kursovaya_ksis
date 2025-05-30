using BlockBlast;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace BlockBlast
{
    public class NetworkManager
    {
        private Server server;
        private Client client;
        private bool isHost;
        private string nickname = "Player";

        public bool IsConnected => client != null && client.IsConnected;

        public event Action<string> OnMessageReceived;
        public event Action<string> OnFigureReceived;


        // === Создание сервера (комната) ===
        public async Task<bool> StartHostAsync(int port = 7777)
        {
            isHost = true;
            server = new Server();
            server.OnMessageReceived += HandleServerMessage;
            server.Start(port);

            // Клиент хоста подключается к себе
            client = new Client();
            client.OnMessageReceived += HandleClientMessage;
            await client.ConnectAsync("127.0.0.1", port);

            return true;
        }

        public bool IsHost
        {
            get { return isHost; }  // Только доступ для чтения
        }

        public void SetNickname(string name)
        {
            nickname = name;
        }

        public string GetNickname()
        {
            return nickname;
        }


        // Метод для установки значения IsHost
        public void SetHost(bool value)
        {
            isHost = value;  // Устанавливаем значение
        }


        private void HandleFigureReceived(string message)
        {
            // Передаем данные в GameWindow через событие
            OnFigureReceived?.Invoke(message);
        }


        private Canvas CreateFigureFromData(string[] figureData)
        {
            var canvas = new Canvas();

            foreach (var item in figureData)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                var coords = item.Trim().Split(',');

                if (coords.Length != 2 ||
                    !double.TryParse(coords[0].Trim(), out double x) ||
                    !double.TryParse(coords[1].Trim(), out double y))
                {
                    MessageBox.Show($"Ошибка преобразования координат: '{item}'");
                    continue;
                }

                var rect = new Rectangle
                {
                    Width = 30,
                    Height = 30,
                    Fill = Brushes.Red,
                    Stroke = Brushes.White,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            }

            return canvas;
        }


        private void NetworkManager_OnFigureReceived(string message)
        {
            HandleFigureReceived(message); // Метод для обработки полученной фигуры
        }



        // === Подключение к комнате ===
        public async Task<bool> ConnectToHostAsync(string ip, int port = 7777)
        {
            isHost = false;
            client = new Client();
            client.OnMessageReceived += HandleClientMessage;

            try
            {
                await client.ConnectAsync(ip, port);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SendNicknameAsync(string nickname)
        {
            string tag = isHost ? "H" : "C";
            string payload = $"{tag}|{nickname}";
            await SendMessageAsync(new NetworkMessage(MessageType.Nickname, payload));
        }



        public async Task SendMessageAsync(NetworkMessage message)
        {
            if (client == null || !client.IsConnected) return;

            var serializedMessage = $"{message.Type}|{message.Payload}"; // Сериализация сообщения в строку
            await client.SendMessageAsync(serializedMessage); // Отправка строки
        }


        // === Получение от клиента (если ты сервер) ===
        private void HandleServerMessage(string message, TcpClient client)
        {
            
            OnMessageReceived?.Invoke(message);
            OnFigureReceived?.Invoke(message);
        }

        public async Task SendScoreAsync(int score)
        {
            string playerTag = isHost ? "H" : "C";
            string message = $"{playerTag}|{score}"; // ❗️ без ScoreUpdate
            await SendMessageAsync(new NetworkMessage(MessageType.ScoreUpdate, message));
        }



        private void HandleClientMessage(string message)
        {

            OnMessageReceived?.Invoke(message);

            if (message.StartsWith("SendFigure") || message.StartsWith("ScoreUpdate") || message.StartsWith("GameOver") || message.StartsWith("Nickname"))
            {
                OnFigureReceived?.Invoke(message);
            }
        }


        public void Shutdown()
        {
            client?.Disconnect();
            server?.Stop();
        }
        
        public async Task SendFigureAsync(Canvas figure, int row, int col)
        {
            // Сериализуем позицию и блоки: "7,5|0,0 1,0 2,0"
            var figureData = SerializeFigureData(figure, row, col);

            // Добавляем тэг отправителя: "H" (host) или "C" (client)
            var playerTag = isHost ? "H" : "C";

            // Финальное сообщение: "SendFigure|H|7,5|0,0 1,0 2,0"
            var payload = $"{playerTag}|{figureData}";

            

            // Отправка через сеть
            await SendMessageAsync(new NetworkMessage(MessageType.SendFigure, payload));
        }

        public async Task SendGameOverAsync()
        {
            string tag = isHost ? "H" : "C";
            string message = $"GameOver|{tag}";
            await SendMessageAsync(new NetworkMessage(MessageType.GameOver, message));
        }


        private string SerializeFigureData(Canvas figure, int row, int col)
        {
            double cellSize = 50;
            double spacing = 4;

            var blockStrings = new List<string>();
            string colorHex = "#FFCCCCCC";

            foreach (UIElement child in figure.Children)
            {
                if (child is Rectangle rect)
                {
                    double x = Canvas.GetLeft(rect);
                    double y = Canvas.GetTop(rect);

                    int dx = (int)Math.Round(x / (cellSize + spacing));
                    int dy = (int)Math.Round(y / (cellSize + spacing));

                    blockStrings.Add($"{dx},{dy}");
                    // Получаем цвет из первого прямоугольника
                    if (rect.Fill is SolidColorBrush brush)
                    {
                        colorHex = brush.Color.ToString(); // e.g. "#FF3C8DBC"
                    }
                }
            }

            return $"{row},{col}|{string.Join(" ", blockStrings)}|{colorHex}"; // 👈 теперь всё в 1 строку
        }





    }
}
