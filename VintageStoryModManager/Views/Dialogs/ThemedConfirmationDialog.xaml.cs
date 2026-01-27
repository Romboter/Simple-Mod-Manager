using System.Windows;

namespace VintageStoryModManager.Views.Dialogs
{
    public partial class ThemedConfirmationDialog : Window
    {
        public ThemedConfirmationDialog(string message, string title)
        {
            InitializeComponent();
            TitleText.Text = title;
            MessageText.Text = message;
            // Set focus to Delete button for keyboard users
            Loaded += (s, e) =>
            {
                if (this.FindName("DeleteButton") is Button btn)
                {
                    btn.Focus();
                }
            };
        }

        public bool UserConfirmed { get; private set; }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            UserConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            UserConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
