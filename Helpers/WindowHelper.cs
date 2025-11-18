using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MechwarriorVRLauncher.Helpers
{
    public static class WindowHelper
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void ApplyDarkModeToTitleBar(Window window, bool useDarkMode)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                int value = useDarkMode ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
            }
            catch
            {
                // Ignore errors
            }
        }

        public static bool IsDarkThemeActive()
        {
            if (Application.Current.Resources.MergedDictionaries.Count > 0)
            {
                var dict = Application.Current.Resources.MergedDictionaries[0];
                if (dict.Source?.OriginalString?.Contains("Dark") == true)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
