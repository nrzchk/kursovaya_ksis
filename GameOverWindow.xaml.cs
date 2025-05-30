using System.Windows;

namespace BlockBlast
{
    public partial class GameOverWindow : Window
    {
        private readonly string currentUsername;
        public GameOverWindow(string resultMessage, int finalScore, string username)
        {
            InitializeComponent();
            ResultText.Text = resultMessage;
            ScoreText.Text = $"Ваш счёт: {finalScore}";
            currentUsername = username;
        }

        private void ReturnToMenu_Click(object sender, RoutedEventArgs e)
        {
            var roomMenu = new RoomMenuWindow(currentUsername);
            roomMenu.Show();
            this.Close();
        }

    }
}
