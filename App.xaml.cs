using BlockBlast;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BlockBlast
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string[] args = Environment.GetCommandLineArgs();

            if (args.Length >= 2)
            {
                string username = args[1];

                // Проверка: существует ли пользователь
                if (!UserManager.IsUserExists(username))
                {
                    MessageBox.Show($"Пользователь с именем \"{username}\" не найден.");
                    Shutdown();
                    return;
                }

                // Открыть окно входа по паролю
                var loginWindow = new LoginWindow(usernameOnly: username); // ← передаём ник
                loginWindow.Show();
            }
            else
            {
                // Без параметров — обычная регистрация
                var loginWindow = new LoginWindow(); // ← без аргументов
                loginWindow.Show();
            }
        }

    }

}
