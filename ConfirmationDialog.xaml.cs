namespace MechwarriorVRLauncher
{
    public partial class ConfirmationDialog : Window
    {
        public bool UserConfirmed { get; private set; } = false;

        public ConfirmationDialog(List<string> itemsToDelete)
        {
            InitializeComponent();

            // Build the text to display
            ItemsTextBlock.Text = string.Join("\n\n", itemsToDelete);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            UserConfirmed = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            UserConfirmed = false;
            Close();
        }
    }
}
