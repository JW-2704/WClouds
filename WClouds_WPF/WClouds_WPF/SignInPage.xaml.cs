using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    /// Interaction logic for SignInPage.xaml
    /// </summary>
    public partial class SignInPage : Page
    {
        public SignInPage()
        {
            InitializeComponent();
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            Authenticator authenticator = new Authenticator();
            string email = emailTextBox.Text;
            string password = passwordTextBox.Password;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen.");
                return;
            }

            try
            {
                LoginResponse result = await authenticator.Login(email, password);
                App.CurrentUserId = result.user_id;
                Webservice.HttpClient.DefaultRequestHeaders.Remove("X-API-Key");
                Webservice.HttpClient.DefaultRequestHeaders.Add("X-API-Key", result.session_key);

                DataPage dataPage = new DataPage();
                MainFrame.Content = dataPage;
                await dataPage.LoadFiles();
            }
            catch
            {
                MessageBox.Show("Login fehlgeschlagen");
            }
        }
    }
}
