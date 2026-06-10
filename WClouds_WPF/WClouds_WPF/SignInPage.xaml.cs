using System;
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
            string email    = emailTextBox.Text;
            string password = passwordTextBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen.");
                return;
            }

            try
            {
                Authenticator authenticator = new Authenticator();

                // Password is hashed inside Authenticator.Login – never sent as plaintext.
                // Authenticator.Login also calls Webservice.SetApiKey internally,
                // so there is no need to set the header again here.
                LoginResponse result = await authenticator.Login(email, password);
                App.CurrentUserId = result.user_id;

                DataPage dataPage = new DataPage();
                MainFrame.Content = dataPage;
                await dataPage.LoadFiles();
            }
            catch
            {
                MessageBox.Show("Login fehlgeschlagen.");
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(passwordTextBox.Password)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
