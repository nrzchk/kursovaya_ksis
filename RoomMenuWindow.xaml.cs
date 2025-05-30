using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;

namespace BlockBlast
{
    public partial class RoomMenuWindow : Window
    {
        Random random = new Random();
        private bool isHostMode;

        private string currentUsername;
        private NetworkManager networkManager;


        public RoomMenuWindow(string username)
        {
            InitializeComponent();
            Loaded += RoomMenuWindow_Loaded;
            currentUsername = username;
        }

        private void RoomMenuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));
            MainPanel.BeginAnimation(OpacityProperty, fadeIn);

            var textAnimation = (Storyboard)FindResource("TextAnimation");
            textAnimation?.Begin();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Shape shape = CreateRandomShape();
            Canvas.SetLeft(shape, random.Next((int)this.Width));
            Canvas.SetTop(shape, this.Height + 50);

            BackgroundCanvas.Children.Add(shape);
            AnimateShape(shape);
        }

        private Shape CreateRandomShape()
        {
            Shape shape;
            int type = random.Next(3);

            switch (type)
            {
                case 0:
                    shape = new Ellipse { Width = 40, Height = 40, Fill = CreateRandomBrush(), Opacity = 0 };
                    break;
                case 1:
                    shape = new Rectangle { Width = 40, Height = 40, Fill = CreateRandomBrush(), Opacity = 0 };
                    break;
                default:
                    shape = new Polygon
                    {
                        Points = new PointCollection { new Point(30, 0), new Point(60, 20), new Point(45, 60), new Point(15, 60), new Point(0, 20) },
                        Fill = CreateRandomBrush(),
                        Width = 60,
                        Height = 60,
                        Opacity = 0
                    };
                    break;
            }

            return shape;
        }

        private Brush CreateRandomBrush()
        {
            return new SolidColorBrush(Color.FromArgb(100, (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        }

        private void AnimateShape(Shape shape)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            shape.BeginAnimation(OpacityProperty, fadeIn);

            var rise = new DoubleAnimation(Canvas.GetTop(shape), -100, TimeSpan.FromSeconds(random.Next(6, 9)));
            rise.Completed += (s, e) => BackgroundCanvas.Children.Remove(shape);
            shape.BeginAnimation(Canvas.TopProperty, rise);
        }

        private void ExitGame_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private async void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            var networkManager = new NetworkManager();
            networkManager.SetNickname(currentUsername);

            await networkManager.StartHostAsync(); // ← сначала подключаемся
            // await networkManager.SendNicknameAsync(currentUsername); // ← потом отправляем ник

            File.WriteAllText("room.flag", "127.0.0.1");

            var gameWindow = new GameWindow(true, networkManager, currentUsername);
            gameWindow.Show();
            this.Close();
        }



        private void JoinRoom_Click(object sender, RoutedEventArgs e)
        {
            var roomListWindow = new RoomListWindow(currentUsername);
            roomListWindow.Show();
            this.Close();
        }


    }
}
