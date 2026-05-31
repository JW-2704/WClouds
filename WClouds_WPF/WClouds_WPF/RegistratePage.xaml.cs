using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WClouds_WPF.Logic;

namespace WClouds_WPF
{
    /// <summary>
    /// Interaction logic for RegistratePage.xaml
    /// </summary>
    public partial class RegistratePage : Page
    {
        public RegistratePage()
        {   
            InitializeComponent();
        }
        private string storageKey = string.Empty;


        private void GetKeyButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe");
        }

        private void SelectKeyFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Storage Plan Key Datei auswählen",
                Filter = "Textdateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                KeyFilePathBox.Text = dialog.FileName;

                storageKey = File.ReadAllText(dialog.FileName).Trim();
            }
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            Authenticator authenticator = new Authenticator();
            DataPage dataPage = new DataPage();

            string email = EmailBox.Text;
            string password = PasswordBox.Password;
            string key = KeyFilePathBox.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storageKey))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen und einen Storage Plan auswählen.");
                return;
            }

            try
            {
                string response = await authenticator.Register(email, password, storageKey);

                RegisterPanel.Visibility = Visibility.Collapsed;
                MainFrame.Content = dataPage;
            }
            catch
            {
                MessageBox.Show("Key ist falsch oder existiert nicht.");
            }

        }
    }
}
