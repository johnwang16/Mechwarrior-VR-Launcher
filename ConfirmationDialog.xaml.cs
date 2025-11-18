using MechwarriorVRLauncher.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;

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

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Set title bar dark mode based on current theme
            WindowHelper.ApplyDarkModeToTitleBar(this, WindowHelper.IsDarkThemeActive());
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
