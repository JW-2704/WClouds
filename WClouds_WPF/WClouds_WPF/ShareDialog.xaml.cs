using Serilog;
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
        private readonly StorageService _storageService = new();

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
                // AI Agent: braucht jetzt den vollen User (inkl. Public Key),
                // um den DEK fuer den Empfaenger zu wrappen.
                User? recipient = await _userService.GetUserByEmail(email);

                if (recipient == null)
                {
                    ShowError("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");
                    return;
                }

                if (string.IsNullOrEmpty(recipient.Public_Key))
                {
                    ShowError("Dieser Benutzer hat keinen Verschlüsselungs-Schlüssel registriert.");
                    return;
                }

                // Eigenen DEK fuer diese Datei holen+entpacken, dann mit dem
                // Public Key des Empfaengers neu wrappen - der Server sieht
                // den rohen DEK dabei nie.
                byte[] dek = await _storageService.GetDek(_fileId);
                string wrappedKeyForRecipient = EncryptionService.WrapKey(dek, recipient.Public_Key);

                await _shareService.ShareFile(
                    MemberID: recipient.Id,
                    WrappedKey: wrappedKeyForRecipient,
                    CanRead: CanReadBox.IsChecked == true,
                    CanWrite: CanWriteBox.IsChecked == true,
                    FileID: _fileId
                );
                Log.Logger.Information("Successfully shared file.");

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