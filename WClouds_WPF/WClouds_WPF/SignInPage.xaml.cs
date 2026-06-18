using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    public partial class SignInPage : Page
    {
        public SignInPage()
        {
            InitializeComponent();
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string email = emailTextBox.Text;
            string password = passwordTextBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen.");
                return;
            }

            try
            {
                Authenticator authenticator = new Authenticator();
                LoginResponse result = await authenticator.Login(email, password);
                App.CurrentUserId = result.user_id;

                // Session für WCloudsSync speichern und Sync-Prozess starten
                SaveSession(result.user_id, result.session_key);
                LaunchSyncProcess();

                DataPage dataPage = new DataPage();
                MainFrame.Content = dataPage;
                await dataPage.LoadFiles();
                Log.Logger.Information("User {Email} logged in successfully.", email);
            }
            catch
            {
                MessageBox.Show("Login fehlgeschlagen.");
                Log.Logger.Warning("Failed login attempt for email: {Email}", email);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(passwordTextBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        private static void SaveSession(int userId, string apiKey)
        {
            string dir  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WClouds");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "session.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                UserId = userId,
                ApiKey = apiKey,
                ApiUrl = Webservice.HttpClient.BaseAddress?.ToString() ?? "http://127.0.0.1:8000"
            }));
        }

        private static void LaunchSyncProcess()
        {
            string syncExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WCloudsSync.exe");
            if (!File.Exists(syncExe)) return;

            // Bereits laufende Instanz nicht doppelt starten
            if (Process.GetProcessesByName("WCloudsSync").Length > 0) return;

            Process.Start(new ProcessStartInfo(syncExe)
            {
                UseShellExecute  = false,
                CreateNoWindow   = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            });
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.ShowStartPage();
            }
        }
    }
}
