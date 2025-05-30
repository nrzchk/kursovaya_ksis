using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BlockBlast
{
    public partial class LoginWindow : Window
    {
        private readonly string forcedUsername;

        public LoginWindow(string usernameOnly = null)
        {
            InitializeComponent();
            forcedUsername = usernameOnly;

            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var fade = (Storyboard)FindResource("FadeIn");
            fade?.Begin();

            if (!string.IsNullOrEmpty(forcedUsername))
            {
                UsernameBox.Text = forcedUsername;
                UsernameBox.IsEnabled = false;
                WelcomeText.Text = $"С возвращением, {forcedUsername}!";
            }
            else
            {
                WelcomeText.Text = "Добро пожаловать!";
            }

            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                UsernameBox.Text = UsernameBox.Tag.ToString();
                UsernameBox.Foreground = Brushes.Gray;
            }
        }

        private void UsernameBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UsernameBox.Text == UsernameBox.Tag.ToString())
            {
                UsernameBox.Text = "";
                UsernameBox.Foreground = Brushes.White;
            }
        }

        private void UsernameBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text))
            {
                UsernameBox.Text = UsernameBox.Tag.ToString();
                UsernameBox.Foreground = Brushes.Gray;
            }
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || username == UsernameBox.Tag.ToString())
            {
                MessageBox.Show("Введите логин и пароль");
                return;
            }

            if (!UserManager.IsUserExists(username))
            {
                UserManager.Register(username, password);
            }
            else if (!UserManager.CheckPassword(username, password))
            {
                MessageBox.Show("Неверный пароль");
                return;
            }

            var menuWindow = new RoomMenuWindow(username);
            menuWindow.Show();
            this.Close();
        }
    }
}
