using System.Collections.Generic;
using System.Windows;
using WClouds_WPF.Logic;
// AI Prompt: i now have created a share button and i want it to pop up a little window where you can type in the email of the user and check read write access
namespace WClouds_WPF
{
    public partial class ShareDialog : Window
    {
        private readonly int _fileId;
        private readonly ShareService _shareService = new();
        private readonly User _userService = new();

        public ShareDialog(int fileId)
        {
            InitializeComponent();
            _fileId = fileId;
        }

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Bitte eine E-Mail-Adresse eingeben.");
                return;
            }

            ShareBtn.IsEnabled = false;
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                int? memberId = await _userService.GetUserIdByEmail(email);

                if (memberId == null)
                {
                    ShowError("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");
                    return;
                }

                await _shareService.ShareFile(
                    MemberIDs: new List<int> { memberId.Value },
                    CanRead: CanReadBox.IsChecked == true,
                    CanWrite: CanWriteBox.IsChecked == true,
                    FileID: _fileId
                );

                DialogResult = true;
                Close();
            }
            catch
            {
                ShowError("Teilen fehlgeschlagen. Bitte erneut versuchen.");
            }
            finally
            {
                ShareBtn.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}