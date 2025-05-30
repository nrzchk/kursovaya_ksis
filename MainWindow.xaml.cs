using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BlockBlast
{
    public partial class MainWindow : Window
    {
        Random random = new Random();
        Storyboard clickStoryboard;
        private NetworkManager networkManager;
        private bool isHostMode;
        private string username;

        public MainWindow(string username)
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            networkManager = new NetworkManager();
            isHostMode = true; // по умолчанию клиент
            this.username = username;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления панели
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));
            MainPanel.BeginAnimation(OpacityProperty, fadeIn);

            // Запуск анимации
            var textAnimation = (Storyboard)FindResource("TextAnimation");
            textAnimation?.Begin();  // Убедись, что анимация найдена

            // Таймер на спавн фигур
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
                case 0: // Круг
                    shape = new Ellipse()
                    {
                        Width = random.Next(30, 60),
                        Height = random.Next(30, 60),
                        Fill = CreateRandomBrush(),
                        Opacity = 0
                    };
                    break;
                case 1: // Квадрат
                    shape = new Rectangle()
                    {
                        Width = random.Next(30, 60),
                        Height = random.Next(30, 60),
                        Fill = CreateRandomBrush(),
                        Opacity = 0
                    };
                    break;
                default: // Пятиугольник
                    shape = new Polygon()
                    {
                        Points = new PointCollection()
                        {
                            new Point(30,0), new Point(60,20),
                            new Point(45,60), new Point(15,60),
                            new Point(0,20)
                        },
                        Fill = CreateRandomBrush(),
                        Width = 60,
                        Height = 60,
                        Opacity = 0
                    };
                    break;
            }

            shape.MouseDown += Shape_MouseDown;
            return shape;
        }

        private Brush CreateRandomBrush()
        {
            return new SolidColorBrush(Color.FromArgb(100,
                (byte)random.Next(256),
                (byte)random.Next(256),
                (byte)random.Next(256)));
        }

        private void AnimateShape(Shape shape)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            shape.BeginAnimation(OpacityProperty, fadeIn);

            var rise = new DoubleAnimation(Canvas.GetTop(shape), -100, TimeSpan.FromSeconds(random.Next(6, 9)));
            rise.Completed += (s, e) => BackgroundCanvas.Children.Remove(shape);

            shape.BeginAnimation(Canvas.TopProperty, rise);
        }

        private void Shape_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Shape shape)
            {
                var explode = new DoubleAnimation(1, 3, TimeSpan.FromSeconds(0.3));
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                fadeOut.Completed += (s, ev) => BackgroundCanvas.Children.Remove(shape);

                shape.RenderTransformOrigin = new Point(0.5, 0.5);
                shape.RenderTransform = new ScaleTransform();

                shape.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, explode);
                shape.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, explode);
                shape.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            btn.RenderTransformOrigin = new Point(0.5, 0.5);

            if (!(btn.RenderTransform is ScaleTransform))
            {
                btn.RenderTransform = new ScaleTransform(1, 1);
            }

            var scaleX = new DoubleAnimation()
            {
                To = 0.9,
                Duration = TimeSpan.FromSeconds(0.1),
                AutoReverse = true
            };

            var scaleY = new DoubleAnimation()
            {
                To = 0.9,
                Duration = TimeSpan.FromSeconds(0.1),
                AutoReverse = true
            };

            var storyboard = new Storyboard();
            Storyboard.SetTarget(scaleX, btn);
            Storyboard.SetTarget(scaleY, btn);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.ScaleX"));
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.ScaleY"));
            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);

            storyboard.Completed += async (s, ev) =>
            {
                try
                {
                    if (isHostMode)
                        await networkManager.StartHostAsync();
                    else
                        await networkManager.ConnectToHostAsync("127.0.0.1");

                    var roomMenu = new RoomMenuWindow(username);
                    roomMenu.Show();
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка запуска сети: " + ex.Message);
                }
            };

            storyboard.Begin();
        }


    }
}
