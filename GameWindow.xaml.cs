using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.IO;

namespace BlockBlast
{
    public partial class GameWindow : Window
    {
        private NetworkManager networkManager;
        private bool isHostMode;
        private string currentUsername;


        public GameWindow(bool isHost, NetworkManager existingNetworkManager, string username)
        {
            InitializeComponent();
            this.Closed += GameWindow_Closed;
            this.isHostMode = isHost;
            this.networkManager = existingNetworkManager;
            this.currentUsername = username;
            networkManager.SetHost(isHost);  // Устанавливаем хост через метод SetHost                                             // Устанавливаем свойство IsHost
            networkManager.OnFigureReceived += NetworkManager_OnFigureReceived;
            networkManager.OnMessageReceived += NetworkManager_OnMessageReceived;


            Loaded += Window_Loaded;

        }

        private Rectangle[,] opponentCells = new Rectangle[9, 9];

        private int score = 0;
        private Random random = new Random();
        private bool isDragging = false;
        private Point mouseStart;
        private Point figureStart;
        private Canvas draggedFigure;
        private readonly Color[] availableColors = new Color[]
{
    Colors.DeepSkyBlue,
    Colors.MediumVioletRed,
    Colors.LimeGreen,
    Colors.Gold,
    Colors.Orange,
    Colors.MediumPurple
};
        private UIElement draggedElement;
        private Point mouseOffset;
        private Canvas dragCanvas; // Временное хранилище для перемещаемой фигуры
        private Point originalPosition; // Координаты возврата
        private Panel originalParent; // Панель, из которой была взята фигура
        private int originalIndex;


        private Rectangle[,] gridCells = new Rectangle[9, 9];
        private bool[,] cellOccupied = new bool[9, 9];

        private List<(List<(int x, int y)> blocks, (int offsetX, int offsetY) topLeft)> figureTemplates = new List<(List<(int x, int y)> blocks, (int offsetX, int offsetY) topLeft)>
{
    // 1. Один блок
    (new List<(int x, int y)> { (0, 0) }, (0, 0)),

    // 2. Два блока в ряд
    (new List<(int x, int y)> { (0, 0), (1, 0) }, (0, 0)),

    // 3. Три блока в ряд
    (new List<(int x, int y)> { (0, 0), (1, 0), (2, 0) }, (0, 0)),

    // 4. Квадрат 2x2
    (new List<(int x, int y)> { (0, 0), (1, 0), (0, 1), (1, 1) }, (0, 0)),

    // 5. Прямоугольник 2x3
    (new List<(int x, int y)> { (0, 0), (1, 0), (0, 1), (1, 1), (0, 2), (1, 2) }, (0, 0)),

    // 6. Фигура "Т" с верхней ножкой
    (new List<(int x, int y)> { (0, 0), (1, 0), (2, 0), (1, -1) }, (0, 0)),

    // 7. Фигура "Т" с нижней ножкой
    (new List<(int x, int y)> { (0, 0), (1, 0), (2, 0), (1, 1) }, (0, 0)),

    // 8. Угол └
    (new List<(int x, int y)> { (0, 0), (0, 1), (1, 1) }, (0, 0)),

    // 9. Угол ┌
    (new List<(int x, int y)> { (0, 0), (0, -1), (1, -1) }, (0, 0)),

    // 10. Угол ┘
    (new List<(int x, int y)> { (0, 0), (1, 0), (1, -1) }, (0, 0)),

    // 11. Угол ┐
    (new List<(int x, int y)> { (0, 0), (1, 0), (1, 1) }, (0, 0)),

    // 12. Логика для других фигур аналогична...
    // Здесь все фигуры будут иметь привязку к левому верхнему углу
};

        // Метод для получения левого верхнего угла
        private (int offsetX, int offsetY) GetTopLeft(List<(int x, int y)> blocks)
        {
            int minX = blocks.Min(block => block.x);
            int minY = blocks.Min(block => block.y);

            return (minX, minY);
        }

        private Point mouseOffsetInFigure;

        private void GameWindow_Closed(object sender, EventArgs e)
        {
            // 👉 Удаляем файл-маркер при закрытии
            if (File.Exists("room.flag"))
                File.Delete("room.flag");
        }
        private void NetworkManager_OnFigureReceived(string message)
        {
            
            

        }

        private async void PlaceFigure(int row, int col, Canvas figure)
        {
            // Логика для обновления местоположения фигуры
            UpdateFigureOnGrid(row, col, figure);

            // Отправка фигуры противнику
            if (networkManager.IsConnected)
            {
                await networkManager.SendFigureAsync(figure, row, col); // Отправляем фигуру другому игроку
            }
        }


        private void HandleFigureReceived(string message)
        {
            var parts = message.Split('|');
            if (parts.Length < 4)
            {
                MessageBox.Show("Некорректное сообщение");
                return;
            }

            string senderTag = parts[1];

            // Пропускаем сообщение, если оно от самого себя
            if ((senderTag == "H" && isHostMode) || (senderTag == "C" && !isHostMode))
            {
                return;
            }

            var position = parts[2].Trim().Split(',');
            if (position.Length != 2)
            {
                MessageBox.Show($"Некорректная позиция: '{parts[2]}'");
                return;
            }

            if (!int.TryParse(position[0], out int row) || !int.TryParse(position[1], out int col))
            {
                MessageBox.Show($"Ошибка разбора координат: {parts[2]}");
                return;
            }

            var blocksRaw = parts[3].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string colorHex = parts.Length >= 5 ? parts[4] : "#FFCCCCCC";

            Dispatcher.Invoke(() =>
            {
                Canvas figure = CreateFigureFromData(blocksRaw, GetOpponentCellSize(), colorHex);
                PlaceFigureOnOpponentGrid(row, col, figure);
            });
        }




        private void NetworkManager_OnMessageReceived(string message)
        {
            // MessageBox.Show("Получено сообщение: " + message);

            if (message.StartsWith("SendFigure"))
            {
                HandleFigureReceived(message);
            }
            else if (message.StartsWith("ScoreUpdate"))
            {
                var parts = message.Split('|');
                if (parts.Length >= 3)
                {
                    string senderTag = parts[1];

                    // Проверяем, что это сообщение от противника
                    if ((senderTag == "H" && !isHostMode) || (senderTag == "C" && isHostMode))
                    {
                        if (int.TryParse(parts[2], out int opponentScore))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                OpponentScoreTextBlock.Text = $"Очки соперника: {opponentScore}";
                            });
                        }
                    }
                }
            }
            else if (message.StartsWith("GameOver"))
            {
                var parts = message.Split('|');
                if (parts.Length >= 2)
                {
                    string senderTag = parts[1];

                    bool isFromOpponent = (senderTag == "H" && !isHostMode) || (senderTag == "C" && isHostMode);

                    if (isFromOpponent)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UserManager.TryUpdateHighScore(currentUsername, score);

                            var winWindow = new GameOverWindow("Вы выиграли!", score, currentUsername);
                            winWindow.ShowDialog();

                            this.Close(); // Закрываем текущее игровое окно
                        });
                    }
                }
            }
            else if (message.StartsWith("Nickname"))
            {
                var parts = message.Split('|');
                if (parts.Length >= 3)
                {
                    string senderTag = parts[1];
                    string nickname = parts[2];

                    bool isFromOpponent = (isHostMode && senderTag == "C") || (!isHostMode && senderTag == "H");

                    if (isFromOpponent)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("Устанавливаем соперника: " + nickname); // временно
                            OpponentNameTextBlock.Text = $"Соперник: {nickname}";
                        });
                    }
                }
            }


        }




        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetupPlayerGrid();
            SetupOpponentGrid();
            GenerateFigures();
            await networkManager.SendNicknameAsync(currentUsername);


            PlayerNameTextBlock.Text = $"Игрок: {currentUsername}";
            OpponentNameTextBlock.Text = $"Соперник: Ожидание...";

        }

        private void SetupOpponentGrid()
        {
            OpponentCanvas.Children.Clear();

            double cellSize = OpponentCanvas.Width / 9;
            double spacing = 0;

            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = cellSize - 1,
                        Height = cellSize - 1,
                        Fill = Brushes.Transparent,
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    };

                    opponentCells[row, col] = rect;

                    Canvas.SetLeft(rect, col * (cellSize + spacing));
                    Canvas.SetTop(rect, row * (cellSize + spacing));

                    OpponentCanvas.Children.Add(rect);
                }
            }

        }



        private void SetupPlayerGrid()
        {
            PlayerGrid.Children.Clear();
            // Просто установите размеры сетки
            PlayerGrid.Rows = 9;
            PlayerGrid.Columns = 9;



            for (int row = 0; row < 9; row++)
            {
                for (int col = 0; col < 9; col++)
                {
                    var cell = new Rectangle
                    {
                        Stroke = Brushes.LightGray,
                        Fill = Brushes.Black,
                        StrokeThickness = 1
                    };
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    PlayerGrid.Children.Add(cell);
                    gridCells[row, col] = cell;
                    cellOccupied[row, col] = false;
                }
            }
        }

        private void GenerateFigures()
        {
            // Очищаем предыдущие фигуры
            FigureSlot1.Children.Clear();
            FigureSlot2.Children.Clear();
            FigureSlot3.Children.Clear();

            var slots = new[] { FigureSlot1, FigureSlot2, FigureSlot3 };

            for (int i = 0; i < 3; i++)
            {
                var figure = CreateRandomFigure();
                figure.RenderTransform = null; // сброс масштаба, если был

                // Удалим старый родитель, если вдруг остался
                if (figure.Parent is Panel oldPanel)
                    oldPanel.Children.Remove(figure);

                slots[i].Children.Add(figure); // Добавляем новую фигуру
            }
        }


        private bool CheckCollision(int startRow, int startCol, Canvas figure)
        {
            double cellSize = PlayerGrid.ActualWidth / 9;
            double spacing = 4; // Отступ между квадратиками

            // Проверяем все клетки, которые фигура должна занять
            foreach (UIElement child in figure.Children)
            {
                if (child is Rectangle rect)
                {
                    double x = Canvas.GetLeft(rect);
                    double y = Canvas.GetTop(rect);

                    // Вычисляем индекс клетки, куда должна быть помещена фигура
                    int dx = (int)((x + spacing / 2) / (cellSize + spacing)); // Используем поправку для точности
                    int dy = (int)((y + spacing / 2) / (cellSize + spacing)); // Используем поправку для точности

                    int finalRow = startRow + dy;
                    int finalCol = startCol + dx;

                    if (finalRow < 0 || finalRow >= 9 || finalCol < 0 || finalCol >= 9)
                        return false; // Проверяем, не выходит ли фигура за пределы поля

                    // Проверка на занятость клеток
                    if (cellOccupied[finalRow, finalCol])
                        return false; // Если клетка занята, то фигуру сюда не поставить
                }
            }

            return true;
        }


        // Разметка занятых клеток для фигуры
        private void MarkPositionsAsOccupied(int startRow, int startCol, Canvas figure)
        {
            double cellSize = PlayerGrid.ActualWidth / 9;
            double spacing = 4;

            foreach (UIElement child in figure.Children)
            {
                if (child is Rectangle rect)
                {
                    double x = Canvas.GetLeft(rect);
                    double y = Canvas.GetTop(rect);

                    int dx = (int)Math.Round(x / (cellSize + spacing));
                    int dy = (int)Math.Round(y / (cellSize + spacing));

                    int finalRow = startRow + dy;
                    int finalCol = startCol + dx;

                    // Обновляем массив занятых клеток
                    cellOccupied[finalRow, finalCol] = true;
                }
            }
        }

        private Canvas CreateFigureFromData(string[] figureData, double cellSize, string colorHex)
        {
            List<(int x, int y)> blocks = new List<(int, int)>();

            foreach (var item in figureData)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                var coords = item.Split(',');

                if (coords.Length < 2)
                {
                    MessageBox.Show($"Неверный формат координат: '{item}'");
                    continue;
                }

                if (int.TryParse(coords[0].Trim(), out int x) &&
                    int.TryParse(coords[1].Trim(), out int y))
                {
                    blocks.Add((x, y));
                }
                else
                {
                    MessageBox.Show($"Ошибка преобразования: '{coords[0]}' или '{coords[1]}'");
                }
            }

            // Преобразуем hex в цвет (например, #FF3C8DBC)
            Color parsedColor;
            try
            {
                parsedColor = (Color)ColorConverter.ConvertFromString(colorHex);
            }
            catch
            {
                MessageBox.Show($"Ошибка чтения цвета: '{colorHex}', используется серый.");
                parsedColor = Colors.Gray; // цвет по умолчанию
                
            }

            var brush = new SolidColorBrush(parsedColor);

            return CreateFigureFromBlocks(blocks, cellSize, 4, brush);
        }



        private Canvas CreateFigureFromBlocks(List<(int x, int y)> blocks, double cellSize = -1, double spacing = 4, SolidColorBrush customBrush = null)
        {
            if (blocks.Count == 0)
            {
                MessageBox.Show("Ошибка: список блоков пустой, фигура не будет создана.");
                return new Canvas();
            }

            if (cellSize <= 0)
            {
                cellSize = PlayerGrid.ActualWidth / 9;
            }

            SolidColorBrush brush;

            if (customBrush != null)
            {
                brush = customBrush;
            }
            else if (availableColors != null && availableColors.Length > 0)
            {
                brush = new SolidColorBrush(availableColors[random.Next(availableColors.Length)]);
            }
            else
            {
                brush = new SolidColorBrush(Colors.Gray);
            }

            int minX = blocks.Min(block => block.x);
            int minY = blocks.Min(block => block.y);

            var canvas = new Canvas
            {
                Width = (cellSize + spacing) * 5,
                Height = (cellSize + spacing) * 5,
                Background = Brushes.Transparent
            };

            foreach (var (x, y) in blocks)
            {
                var rect = new Rectangle
                {
                    Width = cellSize - 0.5,
                    Height = cellSize - 0.5,
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    RadiusX = 6,
                    RadiusY = 6,
                    IsHitTestVisible = false,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 5,
                        ShadowDepth = 1,
                        Opacity = 0.4
                    }
                };

                double xPos = (x - minX) * (cellSize + spacing);
                double yPos = (y - minY) * (cellSize + spacing);

                Canvas.SetLeft(rect, xPos);
                Canvas.SetTop(rect, yPos);
                canvas.Children.Add(rect);
            }

            return canvas;
        }




        private Canvas CreateRandomFigure()
        {
            var template = figureTemplates[random.Next(figureTemplates.Count)];

            var color = availableColors[random.Next(availableColors.Length)];
            var brush = new SolidColorBrush(color);
            var blocks = template.blocks;

            double cellSize = PlayerGrid.ActualWidth / 9;
            double spacing = 4;

            // Вычисляем границы, чтобы отцентрировать фигуру
            int minX = blocks.Min(block => block.x);
            int minY = blocks.Min(block => block.y);

            var canvas = new Canvas
            {
                Width = (cellSize + spacing) * 5,
                Height = (cellSize + spacing) * 5,
                Background = Brushes.Transparent
            };

            foreach (var (x, y) in blocks)
            {
                var rect = new Rectangle
                {
                    Width = cellSize,
                    Height = cellSize,
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 1,
                    RadiusX = 6,
                    RadiusY = 6,
                    IsHitTestVisible = false,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 5,
                        ShadowDepth = 1,
                        Opacity = 0.4
                    }
                };

                double xPos = (x - minX) * (cellSize + spacing);
                double yPos = (y - minY) * (cellSize + spacing);

                Canvas.SetLeft(rect, xPos);
                Canvas.SetTop(rect, yPos);
                canvas.Children.Add(rect);
            }

            canvas.MouseLeftButtonDown += Figure_MouseLeftButtonDown;
            canvas.MouseMove += Figure_MouseMove;
            canvas.MouseLeftButtonUp += Figure_MouseLeftButtonUp;

            return canvas;
        }


        private void UpdateScore()
        {
            // Здесь обновляем счёт (пример)
            score += 10;  // Например, 10 очков за размещение
            ScoreText.Text = $"Счёт: {score}";
        }

        private async void CheckAndClearFullLines()
        {
            List<int> fullRows = new List<int>();
            List<int> fullCols = new List<int>();

            // Проверяем строки
            for (int row = 0; row < 9; row++)
            {
                bool isFull = true;
                for (int col = 0; col < 9; col++)
                {
                    if (!cellOccupied[row, col])
                    {
                        isFull = false;
                        break;
                    }
                }

                if (isFull) fullRows.Add(row);
            }

            // Проверяем столбцы
            for (int col = 0; col < 9; col++)
            {
                bool isFull = true;
                for (int row = 0; row < 9; row++)
                {
                    if (!cellOccupied[row, col])
                    {
                        isFull = false;
                        break;
                    }
                }

                if (isFull) fullCols.Add(col);
            }

            // Очищаем строки
            foreach (int row in fullRows)
            {
                for (int col = 0; col < 9; col++)
                {
                    gridCells[row, col].Fill = Brushes.Black;
                    cellOccupied[row, col] = false;
                }
                score += 100; // +100 за строку
            }

            // Очищаем столбцы
            foreach (int col in fullCols)
            {
                for (int row = 0; row < 9; row++)
                {
                    gridCells[row, col].Fill = Brushes.Black;
                    cellOccupied[row, col] = false;
                }
                score += 100; // +100 за столбец
            }

            // Обновляем текст очков
            ScoreText.Text = $"Счёт: {score}";
            await networkManager.SendScoreAsync(score);
        }

        private bool CanPlaceAnyFigure()
        {
            var figureSlots = new[] { FigureSlot1, FigureSlot2, FigureSlot3 };

            foreach (var slot in figureSlots)
            {
                if (slot.Children.Count > 0)
                {
                    var figure = slot.Children[0] as Canvas;
                    if (figure == null)
                    {
                        MessageBox.Show("Фигура в слоте — не Canvas!");
                        continue;
                    }

                    // Сначала определим максимальный dx и dy — размер фигуры
                    int maxDx = 0;
                    int maxDy = 0;
                    double cellSize = PlayerGrid.ActualWidth / 9;
                    double spacing = 4;

                    foreach (UIElement child in figure.Children)
                    {
                        if (child is Rectangle rect)
                        {
                            double x = Canvas.GetLeft(rect);
                            double y = Canvas.GetTop(rect);
                            int dx = (int)((x + spacing / 2) / (cellSize + spacing));
                            int dy = (int)((y + spacing / 2) / (cellSize + spacing));
                            if (dx > maxDx) maxDx = dx;
                            if (dy > maxDy) maxDy = dy;
                        }
                    }

                    // Проверяем возможные позиции размещения
                    for (int row = 0; row <= 9 - (maxDy + 1); row++)
                    {
                        for (int col = 0; col <= 9 - (maxDx + 1); col++)
                        {
                            if (CheckCollision(row, col, figure))
                                return true;
                        }
                    }
                }
            }

            return false;
        }




        private void Figure_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            draggedElement = sender as UIElement;
            if (draggedElement == null) return;

            draggedFigure = draggedElement as Canvas;
            if (draggedFigure == null) return;

            originalParent = VisualTreeHelper.GetParent(draggedElement) as Panel;
            if (originalParent == null) return;

            originalIndex = originalParent.Children.IndexOf(draggedElement);
            originalPosition = draggedElement.TranslatePoint(new Point(0, 0), MainCanvas);

            // Получаем координаты левого верхнего угла фигуры
            var template = figureTemplates[random.Next(figureTemplates.Count)];
            var blocks = template.blocks;
            int minX = blocks.Min(block => block.x);
            int minY = blocks.Min(block => block.y);

            // Сохраняем смещение от реального левого верхнего угла фигуры
            mouseOffset = e.GetPosition(draggedElement);
            mouseOffset = new Point(mouseOffset.X - minX, mouseOffset.Y - minY); // Привязываем курсор к левому верхнему углу

            originalParent.Children.Remove(draggedElement);

            // Создаем Canvas, чтобы держать элемент поверх всего
            if (dragCanvas == null)
            {
                dragCanvas = new Canvas();
                Panel.SetZIndex(dragCanvas, 999);
                MainCanvas.Children.Add(dragCanvas);
            }

            dragCanvas.Children.Add(draggedElement);
            Canvas.SetLeft(draggedElement, originalPosition.X - mouseOffset.X);
            Canvas.SetTop(draggedElement, originalPosition.Y - mouseOffset.Y);

            Mouse.Capture(draggedElement);
        }

        private void Figure_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedElement == null || e.LeftButton != MouseButtonState.Pressed) return;

            // Получаем текущее положение мыши относительно главного канваса
            Point position = e.GetPosition(MainCanvas);

            // Перемещаем фигуру так, чтобы её левый верхний угол был привязан к курсору
            Canvas.SetLeft(draggedElement, position.X - mouseOffset.X);
            Canvas.SetTop(draggedElement, position.Y - mouseOffset.Y);
        }

        private double GetCellSize()
        {
            return PlayerGrid.ActualWidth / 9;
        }


        private void UpdateFigureOnGrid(int row, int col, Canvas figure)
        {
            double cellSize = PlayerGrid.ActualWidth / 9;
            double spacing = 4;

            foreach (UIElement child in figure.Children)
            {
                if (child is Rectangle rect)
                {
                    double x = Canvas.GetLeft(rect);
                    double y = Canvas.GetTop(rect);

                    // Вычисляем координаты на игровом поле и обновляем ячейку
                    int dx = (int)((x + spacing / 2) / (cellSize + spacing));
                    int dy = (int)((y + spacing / 2) / (cellSize + spacing));

                    int finalRow = row + dy;
                    int finalCol = col + dx;

                    if (finalRow >= 0 && finalRow < 9 && finalCol >= 0 && finalCol < 9)
                    {
                        Rectangle cell = gridCells[finalRow, finalCol];
                        cell.Fill = rect.Fill;  // Используем цвет фигуры
                        cellOccupied[finalRow, finalCol] = true; // Отмечаем клетку как занятую
                    }
                }
            }
        }


        private async void Figure_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (draggedFigure == null) return;

            // Получаем положение мыши относительно поля
            Point mousePosition = e.GetPosition(PlayerGrid);
            double cellSize = PlayerGrid.ActualWidth / 9;

            // Преобразуем положение мыши в клетку
            int col = (int)(mousePosition.X / cellSize);
            int row = (int)(mousePosition.Y / cellSize);

            // Проверяем, можно ли разместить фигуру в этой клетке
            if (row >= 0 && row < 9 && col >= 0 && col < 9 &&
                CheckCollision(row, col, draggedFigure))
            {
                // Размещаем фигуру с учетом её размера и левого верхнего угла
                double correctedX = col * (cellSize + 4); // Сдвиг в клетку по оси X
                double correctedY = row * (cellSize + 4); // Сдвиг в клетку по оси Y

                // Обновляем игровое поле
                MarkPositionsAsOccupied(row, col, draggedFigure);
                PlaceFigureOnGrid(row, col, draggedFigure);
                UpdateScore();
                CheckAndClearFullLines();
                UserManager.TryUpdateHighScore(currentUsername, score);


                if (networkManager.IsConnected)
                {
                    await networkManager.SendFigureAsync(draggedFigure, row, col); // Отправляем фигуру другому игроку
                }

                // Удаляем использованную фигуру из оригинального слота
                if (originalParent != null && originalParent.Children.Contains(draggedElement))
                {
                    originalParent.Children.Remove(draggedElement);
                }

                // Удаляем временную копию фигуры с верхнего контейнера (MainCanvas)
                if (dragCanvas != null && dragCanvas.Children.Contains(draggedElement))
                {
                    dragCanvas.Children.Remove(draggedElement);
                }

                // Генерируем новые фигуры, если все использованы
                if (FigureSlot1.Children.Count == 0 &&
                    FigureSlot2.Children.Count == 0 &&
                    FigureSlot3.Children.Count == 0)
                {
                    GenerateFigures();
                    bool canPlace = CanPlaceAnyFigure();
                    
                    if (!CanPlaceAnyFigure())
                    {
                        MessageBox.Show("Игра окончена! Нет доступных ходов.");
                        // Вызываем окно окончания игры
                        

                        // Обновляем рекорд
                        UserManager.TryUpdateHighScore(currentUsername, score);

                        await networkManager.SendGameOverAsync();

                        Hide(); // скрываем, но не закрываем
                        var gameOverWindow = new GameOverWindow("Вы проиграли", score, currentUsername);
                        gameOverWindow.ShowDialog(); // ждём, пока пользователь нажмёт "В меню"
                        Close(); // закрываем игру только после закрытия окна


                    }


                }
                else
                {
                    // Если не все фигуры использованы, но ни одну уже нельзя поставить
                    if (!CanPlaceAnyFigure())
                    {
                        await networkManager.SendGameOverAsync();
                        Hide();
                        var gameOverWindow = new GameOverWindow("Вы проиграли", score, currentUsername);
                        gameOverWindow.ShowDialog();
                        Close();
                    }

                }

                // Перемещаем фигуру в точку с учетом исправленных координат
                Canvas.SetLeft(draggedElement, correctedX);
                Canvas.SetTop(draggedElement, correctedY);
            }
            else
            {
                // Возвращаем фигуру на исходную позицию, если она не поместилась
                if (dragCanvas != null && dragCanvas.Children.Contains(draggedElement))
                {
                    dragCanvas.Children.Remove(draggedElement);
                }

                if (originalParent != null)
                {
                    originalParent.Children.Insert(originalIndex, draggedElement);
                }
            }

            // Завершаем процесс перетаскивания
            isDragging = false;
            draggedElement = null;
            draggedFigure = null;
            Mouse.Capture(null); // Освобождаем мышь после отпускания
        }


        private void PlaceFigureOnOpponentGrid(int startRow, int startCol, Canvas figure)
        {
            double cellSize = OpponentCanvas.Width / 9;
            double spacing = 4;

            foreach (UIElement child in figure.Children)
            {
                if (child is Rectangle rect)
                {
                    double x = Canvas.GetLeft(rect);
                    double y = Canvas.GetTop(rect);

                    // Преобразуем в относительное смещение внутри фигуры
                    int dx = (int)Math.Round(x / (cellSize + spacing));
                    int dy = (int)Math.Round(y / (cellSize + spacing));

                    int finalRow = startRow + dy;
                    int finalCol = startCol + dx;

                    if (finalRow >= 0 && finalRow < 9 && finalCol >= 0 && finalCol < 9)
                    {
                        // 🎯 Закрашиваем ячейку на поле противника
                        Rectangle cell = opponentCells[finalRow, finalCol];
                        cell.Fill = rect.Fill; // тот же цвет, что у блока фигуры
                    }
                }
            }
        }



        private void PlaceFigureOnGrid(int startRow, int startCol, Canvas figure)
        {
            double cellSize = PlayerGrid.ActualWidth / 9;
            double spacing = 4;

            foreach (UIElement child in figure.Children)
            {
                if (child is Rectangle rect)
                {
                    double x = Canvas.GetLeft(rect);
                    double y = Canvas.GetTop(rect);

                    // С учетом отступов и точности, вычисляем позицию
                    int dx = (int)((x + spacing / 2) / (cellSize + spacing)); // Поправка для точности
                    int dy = (int)((y + spacing / 2) / (cellSize + spacing)); // Поправка для точности

                    int finalRow = startRow + dy;
                    int finalCol = startCol + dx;

                    if (finalRow >= 0 && finalRow < 9 && finalCol >= 0 && finalCol < 9)
                    {
                        Rectangle cell = gridCells[finalRow, finalCol];
                        cell.Fill = rect.Fill; // Используем цвет фигуры
                        cellOccupied[finalRow, finalCol] = true; // Отмечаем клетку как занятую
                    }
                }
            }
        }


        private void ReturnDraggedElement()
        {
            // Убираем перетаскиваемую фигуру из холста
            dragCanvas.Children.Remove(draggedElement);

            // Возвращаем её в исходный контейнер
            originalParent.Children.Insert(originalIndex, draggedElement);

            // После возвращения перетаскиваемой фигуры обновляем список фигур
            GenerateFigures();
        }



        private bool IsDropValid(Point pos, out int row, out int col)
        {
            row = col = -1;

            Point gridPos = PlayerGrid.TranslatePoint(new Point(0, 0), MainCanvas);
            double x = pos.X - gridPos.X;
            double y = pos.Y - gridPos.Y;

            double cellSize = PlayerGrid.ActualWidth / 9;
            
            if (x < 0 || y < 0) return false;

            col = (int)(x / cellSize);
            row = (int)(y / cellSize);

            if (row < 0 || row >= 9 || col < 0 || col >= 9) return false;

            return true;
        }





        private bool TryPlaceFigure(Canvas figure, int row, int col)
        {
            double cellSize = PlayerGrid.ActualWidth / 9;

            // Проверяем, можно ли разместить фигуру
            if (row < 0 || row >= 9 || col < 0 || col >= 9) return false;

            // Попытка разместить фигуру
            if (CanPlaceFigure(figure, row, col, out var positions))
            {
                foreach (var (r, c) in positions)
                {
                    // Заполняем клетку
                    Rectangle cell = gridCells[r, c];
                    if (figure.Children[0] is Rectangle referenceRect)
                    {
                        cell.Fill = referenceRect.Fill; // Используем цвет фигуры
                    }
                    cellOccupied[r, c] = true; // Отмечаем клетку как занятую
                }

                // Обновляем счёт
                score += positions.Count;
                ScoreText.Text = $"Счёт: {score}";

                // Убираем фигуру с холста
                if (figure.Parent is Panel panel)
                {
                    panel.Children.Remove(figure);
                }

                return true;
            }

            return false;
        }


        private bool CanPlaceFigure(Canvas figure, int targetRow, int targetCol, out List<(int row, int col)> figureCells)
        {
            figureCells = new List<(int, int)>();

            double cellSize = PlayerGrid.ActualWidth / 9;
            double spacing = 4; // тот же spacing, как при создании фигуры

            foreach (UIElement child in figure.Children)
            {
                if (child is Rectangle rect)
                {
                    double x = Canvas.GetLeft(rect);
                    double y = Canvas.GetTop(rect);

                    int dx = (int)Math.Floor(x / (cellSize + spacing));
                    int dy = (int)Math.Floor(y / (cellSize + spacing));


                    int finalRow = targetRow + dy;
                    int finalCol = targetCol + dx;

                    if (finalRow >= 0 && finalRow < 9 && finalCol >= 0 && finalCol < 9)
                    {
                        Rectangle cell = gridCells[finalRow, finalCol];
                        cell.Fill = rect.Fill;
                        cellOccupied[finalRow, finalCol] = true;
                    }


                    figureCells.Add((finalRow, finalCol));
                }
            }

            return true;
        }


        private void ClearLines()
        {
            List<int> fullRows = new List<int>();
            List<int> fullCols = new List<int>();

            for (int r = 0; r < 9; r++)
            {
                bool full = true;
                for (int c = 0; c < 9; c++)
                    if (!cellOccupied[r, c]) full = false;
                if (full) fullRows.Add(r);
            }

            for (int c = 0; c < 9; c++)
            {
                bool full = true;
                for (int r = 0; r < 9; r++)
                    if (!cellOccupied[r, c]) full = false;
                if (full) fullCols.Add(c);
            }

            foreach (int r in fullRows)
                for (int c = 0; c < 9; c++)
                {
                    cellOccupied[r, c] = false;
                    gridCells[r, c].Fill = Brushes.Black;
                }

            foreach (int c in fullCols)
                for (int r = 0; r < 9; r++)
                {
                    cellOccupied[r, c] = false;
                    gridCells[r, c].Fill = Brushes.Black;
                }

            int cleared = fullRows.Count + fullCols.Count;
            if (cleared > 0)
            {
                score += cleared * 10;
                ScoreText.Text = $"Счёт: {score}";
            }

            // Генерация новых фигур только после очистки
            GenerateFigures();
        }

        private double GetOpponentCellSize()
        {
            if (Dispatcher.CheckAccess())
            {
                return OpponentCanvas.Width / 9.0;
            }

            return Dispatcher.Invoke(() => OpponentCanvas.Width / 9.0);
        }





        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
