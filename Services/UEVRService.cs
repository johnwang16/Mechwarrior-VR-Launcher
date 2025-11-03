using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MechwarriorVRLauncher.Services
{
    public class UEVRKeybindings
    {
        public int ShowCursorKeyCode { get; set; }
        public int MenuKeyCode { get; set; }
        public int ResetViewKeyCode { get; set; }
    }

    public class UEVRService
    {
        private readonly LoggingService _loggingService;

        // Windows API imports for DLL injection
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize,
            uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
            uint nSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize,
            IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        public UEVRService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public bool InjectDll(Process targetProcess, string dllPath)
        {
            try
            {
                // Get handle to target process
                IntPtr hProcess = OpenProcess(Constants.ProcessAll, false, targetProcess.Id);
                if (hProcess == IntPtr.Zero)
                {
                    _loggingService.LogMessage($"Failed to open process (PID: {targetProcess.Id})");
                    return false;
                }

                try
                {
                    // Get address of LoadLibraryA
                    IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
                    if (hKernel32 == IntPtr.Zero)
                    {
                        _loggingService.LogMessage("Failed to get handle to kernel32.dll");
                        return false;
                    }

                    IntPtr addrLoadLibrary = GetProcAddress(hKernel32, "LoadLibraryA");
                    if (addrLoadLibrary == IntPtr.Zero)
                    {
                        _loggingService.LogMessage("Failed to get address of LoadLibraryA");
                        return false;
                    }

                    // Allocate memory in target process
                    byte[] dllPathBytes = Encoding.ASCII.GetBytes(dllPath + "\0");
                    IntPtr allocMem = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)dllPathBytes.Length,
                        Constants.MemCommit | Constants.MemReserve, Constants.PageReadWrite);

                    if (allocMem == IntPtr.Zero)
                    {
                        _loggingService.LogMessage("Failed to allocate memory in target process");
                        return false;
                    }

                    // Write DLL path to allocated memory
                    if (!WriteProcessMemory(hProcess, allocMem, dllPathBytes, (uint)dllPathBytes.Length, out _))
                    {
                        _loggingService.LogMessage("Failed to write DLL path to target process memory");
                        return false;
                    }

                    // Create remote thread to load the DLL
                    IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, addrLoadLibrary, allocMem, 0, IntPtr.Zero);
                    if (hThread == IntPtr.Zero)
                    {
                        _loggingService.LogMessage("Failed to create remote thread");
                        return false;
                    }

                    // Wait for the thread to complete (with timeout)
                    uint waitResult = WaitForSingleObject(hThread, 5000);
                    CloseHandle(hThread);

                    if (waitResult == Constants.WaitTimeout)
                    {
                        _loggingService.LogMessage("Warning: LoadLibrary thread timed out");
                    }

                    return true;
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage($"Exception during DLL injection: {ex.Message}");
                return false;
            }
        }

        public UEVRKeybindings? ReadExistingConfig(string configDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(configDirectory) || !Directory.Exists(configDirectory))
                {
                    return null;
                }

                var configFilePath = Path.Combine(configDirectory, Constants.UevrConfigFile);
                if (!File.Exists(configFilePath))
                {
                    return null;
                }

                _loggingService.LogMessage($"Reading existing UEVR config from {configFilePath}");

                var keybindings = new UEVRKeybindings
                {
                    ShowCursorKeyCode = 36,  // Default: Home
                    MenuKeyCode = 45,        // Default: Insert
                    ResetViewKeyCode = 120   // Default: F9
                };

                var lines = File.ReadAllLines(configFilePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith($"{Constants.ConfigShowCursorKey}="))
                    {
                        if (int.TryParse(line.Substring($"{Constants.ConfigShowCursorKey}=".Length), out int keyCode))
                        {
                            keybindings.ShowCursorKeyCode = keyCode;
                            _loggingService.LogMessage($"Found ShowCursorKey: {keyCode}");
                        }
                    }
                    else if (line.StartsWith($"{Constants.ConfigMenuKey}="))
                    {
                        if (int.TryParse(line.Substring($"{Constants.ConfigMenuKey}=".Length), out int keyCode))
                        {
                            keybindings.MenuKeyCode = keyCode;
                            _loggingService.LogMessage($"Found MenuKey: {keyCode}");
                        }
                    }
                    else if (line.StartsWith($"{Constants.ConfigResetViewKey}="))
                    {
                        if (int.TryParse(line.Substring($"{Constants.ConfigResetViewKey}=".Length), out int keyCode))
                        {
                            keybindings.ResetViewKeyCode = keyCode;
                            _loggingService.LogMessage($"Found ResetStandingOriginKey: {keyCode}");
                        }
                    }
                }

                _loggingService.LogMessage($"Loaded existing UEVR keybindings from {Constants.UevrConfigFile}");
                return keybindings;
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage($"Could not read existing UEVR config: {ex.Message}");
                // Not a critical error, just continue with defaults
                return null;
            }
        }

        public void ModifyConfigFile(string configDirectory, int showCursorKeyCode, int menuKeyCode, int resetViewKeyCode)
        {
            try
            {
                var configFilePath = Path.Combine(configDirectory, Constants.UevrConfigFile);

                if (!File.Exists(configFilePath))
                {
                    _loggingService.LogMessage($"Warning: {Constants.UevrConfigFile} not found at {configFilePath}");
                    return;
                }

                _loggingService.LogMessage($"Modifying {Constants.UevrConfigFile} with ShowCursorKey={showCursorKeyCode}, MenuKey={menuKeyCode}, ResetViewKey={resetViewKeyCode}");

                var lines = File.ReadAllLines(configFilePath);
                bool foundShowCursor = false;
                bool foundMenu = false;
                bool foundResetView = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith($"{Constants.ConfigShowCursorKey}="))
                    {
                        lines[i] = $"{Constants.ConfigShowCursorKey}={showCursorKeyCode}";
                        foundShowCursor = true;
                        _loggingService.LogMessage($"Updated {Constants.ConfigShowCursorKey} to {showCursorKeyCode}");
                    }
                    else if (lines[i].StartsWith($"{Constants.ConfigMenuKey}="))
                    {
                        lines[i] = $"{Constants.ConfigMenuKey}={menuKeyCode}";
                        foundMenu = true;
                        _loggingService.LogMessage($"Updated {Constants.ConfigMenuKey} to {menuKeyCode}");
                    }
                    else if (lines[i].StartsWith($"{Constants.ConfigResetViewKey}="))
                    {
                        lines[i] = $"{Constants.ConfigResetViewKey}={resetViewKeyCode}";
                        foundResetView = true;
                        _loggingService.LogMessage($"Updated {Constants.ConfigResetViewKey} to {resetViewKeyCode}");
                    }
                }

                var linesList = lines.ToList();

                if (!foundShowCursor)
                {
                    linesList.Add($"{Constants.ConfigShowCursorKey}={showCursorKeyCode}");
                    _loggingService.LogMessage($"Added {Constants.ConfigShowCursorKey}={showCursorKeyCode}");
                }

                if (!foundMenu)
                {
                    linesList.Add($"{Constants.ConfigMenuKey}={menuKeyCode}");
                    _loggingService.LogMessage($"Added {Constants.ConfigMenuKey}={menuKeyCode}");
                }

                if (!foundResetView)
                {
                    linesList.Add($"{Constants.ConfigResetViewKey}={resetViewKeyCode}");
                    _loggingService.LogMessage($"Added {Constants.ConfigResetViewKey}={resetViewKeyCode}");
                }

                File.WriteAllLines(configFilePath, linesList.ToArray());
                _loggingService.LogMessage($"{Constants.UevrConfigFile} updated successfully");
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage($"Error modifying {Constants.UevrConfigFile}: {ex.Message}");
            }
        }
    }
}
