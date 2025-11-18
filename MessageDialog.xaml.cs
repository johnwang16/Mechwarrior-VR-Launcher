using MechwarriorVRLauncher.Helpers;
using System;
using System.Windows;

namespace MechwarriorVRLauncher
{
    public enum MessageDialogButton
    {
        OK,
        YesNo
    }

    public enum MessageDialogResult
    {
        OK,
        Yes,
        No
    }

    public partial class MessageDialog : Window
    {
        public MessageDialogResult Result { get; private set; } = MessageDialogResult.OK;

        public MessageDialog(string message, string title, MessageDialogButton button = MessageDialogButton.OK)
        {
            InitializeComponent();

            this.Title = title;
            MessageTextBlock.Text = message;

            // Configure buttons based on button type
            if (button == MessageDialogButton.YesNo)
            {
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Collapsed;
                YesButton.IsDefault = true;
            }
            else
            {
                YesButton.Visibility = Visibility.Collapsed;
                NoButton.Visibility = Visibility.Collapsed;
                OkButton.Visibility = Visibility.Visible;
                OkButton.IsDefault = true;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Set title bar dark mode based on current theme
            WindowHelper.ApplyDarkModeToTitleBar(this, WindowHelper.IsDarkThemeActive());
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageDialogResult.OK;
            DialogResult = true;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageDialogResult.Yes;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageDialogResult.No;
            DialogResult = false;
            Close();
        }
    }
}
