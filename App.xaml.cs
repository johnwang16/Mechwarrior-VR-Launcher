namespace MechwarriorVRLauncher
{
    public partial class App : Application
    {
        public static bool AutoLaunchAndInject { get; private set; }

        public App()
        {
            this.InitializeComponent();

            // Handle unhandled exceptions
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for command line arguments
            if (e.Args != null && e.Args.Length > 0)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.Equals(Constants.CmdArgLaunchInjectDash, StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals(Constants.CmdArgLaunchInjectSlash, StringComparison.OrdinalIgnoreCase))
                    {
                        AutoLaunchAndInject = true;
                        break;
                    }
                }
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Log the error
            LogError($"Unhandled exception: {e.Exception.Message}\n{e.Exception.StackTrace}");
            e.Handled = false; // Let it crash so user knows something is wrong
        }

        private void LogError(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MechwarriorVRLauncher",
                    "error.log");

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] {message}\n\n");
            }
            catch
            {
                // Can't log, oh well
            }
        }
    }
}
