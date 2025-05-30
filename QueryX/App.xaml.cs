using QueryX.Services; // Reference Services
using QueryX.ViewModels; // Reference ViewModels
using QueryX.Views; // Reference Views
using QueryX.Logging; // Reference Logging
using Serilog; // Reference Serilog for logging
using System;
using System.IO; // Required for Path operations
using System.Windows; // Required for Application, StartupEventArgs
using System.Windows.Threading; // Required for Dispatcher

namespace QueryX // Ensure namespace matches your project
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Override the OnStartup method, which runs when the application starts
        protected override void OnStartup(StartupEventArgs e)
        {
            // Base implementation executes standard startup procedures
            base.OnStartup(e);

            // 1. Initialize the logger
            var configManager = new ConfigurationManager(); // Need this to find our AppData path
            string logFilePath = Path.Combine(configManager.AppDataFolderPath, "Logs", "QueryX-.log");
            Logging.Log.Initialize(logFilePath);
            Logging.Log.Logger?.Information("=========================================");
            Logging.Log.Logger?.Information("Application Starting Up...");

            // --- 2. Setup Global Exception Handler ---
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 3. Create the MainViewModel
            //    (Later, this could come from a Dependency Injection container)
            var mainViewModel = new MainViewModel();

            // 4. Load the configuration *synchronously* during startup
            //    We execute the command directly here.
            //    Consider adding error handling or user feedback if loading fails critically.
            mainViewModel.LoadConfigurationCommand.Execute(null);

            // 5. Create the MainWindow
            var mainWindow = new MainWindow();

            // 6. Set the DataContext of the MainWindow to our MainViewModel instance
            //    This is the crucial step that connects the View to the ViewModel
            mainWindow.DataContext = mainViewModel;

            // 7. Show the MainWindow
            mainWindow.Show();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Log the unhandled exception
            Logging.Log.Logger?.Fatal(e.Exception, "An unhandled exception occurred!");

            // Show a user-friendly error message
            MessageBox.Show(
                "An unexpected error occurred. The application may become unstable.\n\nPlease check the log files for more details.\n\nError: " + e.Exception.Message,
                "Unhandled Exception",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Prevent the application from crashing
            e.Handled = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logging.Log.Logger?.Information("Application Shutting Down.");

            // This is a failsafe to catch any in-memory changes before closing.
            if (MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                if (mainViewModel.SaveConfigurationCommand.CanExecute(null))
                {
                    mainViewModel.SaveConfigurationCommand.Execute(null);
                    Logging.Log.Logger?.Information("Final configuration saved on application exit.");
                }
            }

            Logging.Log.Logger?.Information("=========================================\n");
            base.OnExit(e);
        }

    }
}