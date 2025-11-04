using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace MechwarriorVRLauncher.Services
{
    public class KeycodeService
    {
        // Windows API imports for key name conversion
        [DllImport("user32.dll")]
        private static extern int ToUnicode(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeConst = 64)]
            StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public int ConvertWpfKeyToVirtualKeyCode(Key key)
        {
            // Use KeyInterop to convert WPF Key to Virtual Key code
            // Virtual Key codes match JavaScript keycodes for most keys
            return KeyInterop.VirtualKeyFromKey(key);
        }

        public string GetKeyName(Key key, int keyCode)
        {
            // Check if it's a numpad key
            bool isNumpad = key >= Key.NumPad0 && key <= Key.Divide;

            // Try to get printable character first (for non-numpad keys)
            if (!isNumpad)
            {
                string printableChar = TryGetPrintableChar(keyCode);
                if (!string.IsNullOrEmpty(printableChar))
                {
                    return printableChar;
                }
            }

            // For non-printable keys or numpad keys, format the key name nicely
            string keyName = key.ToString();

            // Handle special key name mappings
            if (key == Key.Next)
                return "Page Down";
            else if (key == Key.Prior)
                return "Page Up";
            else if (key == Key.Back)
                return "Backspace";

            // Handle numpad keys specially
            if (isNumpad)
            {
                // Convert "Add" to "Numpad +", etc.
                if (key == Key.Add)
                    return "Numpad+";
                else if (key == Key.Subtract)
                    return "Numpad-";
                else if (key == Key.Multiply)
                    return "Numpad*";
                else if (key == Key.Divide)
                    return "Numpad/";
                else if (key == Key.Decimal)
                    return "Numpad.";
            }

            return keyName;
        }

        public string GetKeyName(int keyCode)
        {
            // Convert keycode back to Key enum for proper display
            try
            {
                Key key = KeyInterop.KeyFromVirtualKey(keyCode);
                if (key != Key.None)
                {
                    return GetKeyName(key, keyCode);
                }
            }
            catch
            {
                // If conversion fails, fall back to generic name
            }

            return "Unknown";
        }

        private string TryGetPrintableChar(int virtualKeyCode)
        {
            try
            {
                var scanCode = MapVirtualKey((uint)virtualKeyCode, 0);
                var keyboardState = new byte[256];
                var buffer = new StringBuilder(64);

                int result = ToUnicode((uint)virtualKeyCode, scanCode, keyboardState, buffer, buffer.Capacity, 0);

                if (result > 0)
                {
                    string character = buffer.ToString();
                    // Only return if it's a printable character
                    if (!string.IsNullOrWhiteSpace(character) && !char.IsControl(character[0]))
                    {
                        return character;
                    }
                }
            }
            catch
            {
                // If conversion fails, return null to fall back to key name
            }

            return null;
        }
    }
}
