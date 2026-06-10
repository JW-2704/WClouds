using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    public partial class RegistratePage : Page
    {
        private string storageKey = string.Empty;

        public RegistratePage()
        {
            InitializeComponent();
        }

        private void SelectKeyFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Storage Plan Key Datei auswählen",
                Filter = "Textdateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                KeyFilePathBox.Text = dialog.FileName;
                storageKey = File.ReadAllText(dialog.FileName).Trim();
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string email    = EmailBox.Text;
            string password = PasswordBox.Password;
            string key      = storageKey;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen und einen Storage Plan auswählen.");
                return;
            }

            try
            {
                Authenticator authenticator = new Authenticator();

                // Password is hashed inside Authenticator.Register – never sent as plaintext
                await authenticator.Register(email, password, key);

                // Navigate to sign-in after successful registration
                MainFrame.Content = new SignInPage();
            }
            catch
            {
                MessageBox.Show("Key ist falsch oder existiert nicht.");
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
