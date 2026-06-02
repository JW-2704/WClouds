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
            DataPage dataPage = new DataPage();
            Authenticator authenticator = new Authenticator();

            string email = emailTextBox.Text;
            string password = passwordTextBox.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Bitte alle Felder ausfüllen.");
                return;
            }
            else
            {
                try
                {
                    string response = await authenticator.Login(email, password);

                    MessageBox.Show("Erfolgreicher Login");
                    MessageBox.Show(response);
                    SignPagePanel.Visibility = Visibility.Collapsed;
                    MainFrame.Content = dataPage;
                }
                catch
                {
                    MessageBox.Show("Login fehlgeschlagen");
                }
            }   
        }
    }
}
