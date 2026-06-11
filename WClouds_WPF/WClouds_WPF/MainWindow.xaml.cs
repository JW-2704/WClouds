using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace WClouds_WPF
{
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void ShowStartPage()
        {
            MainFrame.Content = null;
            StackPage.Visibility = Visibility.Visible;
        }

        private void Sign_In_Click(object sender, RoutedEventArgs e)
        {
            StackPage.Visibility = Visibility.Collapsed;
            MainFrame.Content = new SignInPage();
        }

        private void Registrate_Click(object sender, RoutedEventArgs e)
        {
            StackPage.Visibility = Visibility.Collapsed;
            MainFrame.Content = new RegistratePage();
        }
    }
}
