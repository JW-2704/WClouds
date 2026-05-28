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

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            
            DataPage dataPage = new DataPage();
            Authenticator authenticator = new Authenticator();

            string email = emailTextBox.Text;
            string password = passwordTextBox.Text;

            Task<string> response = authenticator.Login(email, password);

            if (response.IsFaulted)
            {
                MessageBox.Show("Login fehlgeschlagen");
            }
            else
            {
                MessageBox.Show("Erfolgreicher Login");
                SignPagePanel.Visibility = Visibility.Collapsed;
                MainFrame.Content = dataPage;
            }
        }
    }
}
