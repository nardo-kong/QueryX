using QueryX.ViewModels; // Reference ViewModels
using QueryX.Views; // Reference Views
using System.Windows; // Required for Application, StartupEventArgs

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

            // 1. Create the MainViewModel
            //    (Later, this could come from a Dependency Injection container)
            var mainViewModel = new MainViewModel();

            // 2. Load the configuration *synchronously* during startup
            //    We execute the command directly here.
            //    Consider adding error handling or user feedback if loading fails critically.
            mainViewModel.LoadConfigurationCommand.Execute(null);

            // 3. Create the MainWindow
            var mainWindow = new MainWindow();

            // 4. Set the DataContext of the MainWindow to our MainViewModel instance
            //    This is the crucial step that connects the View to the ViewModel
            mainWindow.DataContext = mainViewModel;

            // 5. Show the MainWindow
            mainWindow.Show();
        }
    }
}