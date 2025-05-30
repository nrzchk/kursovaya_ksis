using System.Windows;
using System.IO;
using System;
using System.Threading.Tasks;

namespace BlockBlast
{
    public partial class RoomListWindow : Window
    {
        private readonly string currentUsername;

        public RoomListWindow(string username)
        {
            InitializeComponent();
            currentUsername = username;

            if (File.Exists("room.flag"))
            {
                string ip = File.ReadAllText("room.flag").Trim();
                RoomList.Items.Add($"Комната 1 — IP: {ip}");
            }
        }

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (RoomList.SelectedItem == null)
            {
                MessageBox.Show("Выберите комнату");
                return;
            }

            string selected = RoomList.SelectedItem.ToString();

            // Парсим IP из строки
            string ip = "127.0.0.1"; // по умолчанию
            if (selected.Contains("IP:"))
            {
                var parts = selected.Split(new[] { "IP:" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    ip = parts[1].Trim();
            }

            var networkManager = new NetworkManager();
            networkManager.SetNickname(currentUsername);

            bool connected = await networkManager.ConnectToHostAsync(ip);
            if (connected)
            {
                var gameWindow = new GameWindow(false, networkManager, currentUsername);
                gameWindow.Show();

                // Ждём, пока окно полностью загрузится (1 кадр)
                await Task.Delay(100);

                // Теперь можно безопасно отправить ник
                

                this.Close();
            }


            else
            {
                MessageBox.Show("Не удалось подключиться к комнате.");
            }
        }

    }
}
