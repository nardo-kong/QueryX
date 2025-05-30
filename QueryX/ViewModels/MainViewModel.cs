using QueryX.Models; // Reference Models
using QueryX.Services; // Reference Services
using QueryX.Helpers; // Reference Helpers (for RelayCommand)
using QueryX.Views; // Reference Views (if needed)
using System.Collections.ObjectModel; // For ObservableCollection
using System.Windows.Input; // For ICommand
using System.Linq; // For .ToList()
using System.Windows; // For MessageBox (optional)
using System.Diagnostics;
using QueryX.Logging; // For Debug (optional)

namespace QueryX.ViewModels // Ensure namespace matches your project
{
    public class MainViewModel : ViewModelBase // Inherit from ViewModelBase
    {
        private readonly ConfigurationManager _configurationManager;
        private readonly DatabaseService _databaseService;
        private readonly SqlParser _sqlParser; // <-- Add SqlParser
        private readonly QueryExecutor _queryExecutor; // <-- Add QueryExecutor
        private readonly ExportService _exportService;
        private readonly EncryptionService _encryptionService;
        private readonly SqlValidationService _sqlValidationService;

        private AppConfiguration _appConfig; // Holds the loaded config data

        // Collections exposed to the View for binding
        // Use ObservableCollection<T> so the UI updates automatically when items are added/removed
        public ObservableCollection<DatabaseConnectionInfo> Connections { get; private set; }
        public ObservableCollection<QueryDefinition> Queries { get; private set; }

        private QueryDefinition? _selectedQuery;
        public QueryDefinition? SelectedQuery
        {
            get => _selectedQuery;
            // When a query is selected, we might want to load its parameters into another ViewModel
            set
            {
                if (SetProperty(ref _selectedQuery, value))
                {
                    // Placeholder: Add logic here later to handle query selection change
                    Debug.WriteLine($"Query selected: {value?.Name}");
                    UpdateCurrentQueryExecution();
                }
            }
        }

        private DatabaseConnectionInfo? _activeConnectionForQuery; // Represents the chosen connection for the current query
        // This property might not be strictly needed on MainViewModel if QueryExecutionViewModel handles the choice fully.
        // For now, let's assume it holds the *default* chosen one.
        public DatabaseConnectionInfo? ActiveConnectionForQuery
        {
            get => _activeConnectionForQuery;
            set => SetProperty(ref _activeConnectionForQuery, value);
        }


        // --- Add Property to hold the active Query Execution context ---
        private QueryExecutionViewModel? _currentQueryExecution;
        public QueryExecutionViewModel? CurrentQueryExecution
        {
            get => _currentQueryExecution;
            private set => SetProperty(ref _currentQueryExecution, value); // Make setter private or protected
        }


        // Commands exposed to the View
        public ICommand LoadConfigurationCommand { get; }
        public ICommand SaveConfigurationCommand { get; }
        public ICommand OpenConnectionManagerCommand { get; }
        public ICommand OpenQueryManagerCommand { get; }
        // Add more commands later (Add Connection, Remove Query, Execute Query etc.)

        public MainViewModel()
        {
            _configurationManager = new ConfigurationManager();
            _databaseService = new DatabaseService();
            _encryptionService = new EncryptionService();
            _sqlParser = new SqlParser(); // <-- Instantiate SqlParser
            _queryExecutor = new QueryExecutor(_databaseService, _sqlParser, _encryptionService); // <-- Instantiate QueryExecutor
            _exportService = new ExportService();
            _sqlValidationService = new SqlValidationService(_databaseService);

            _appConfig = new AppConfiguration(); // Start with empty config

            // Initialize observable collections
            Connections = new ObservableCollection<DatabaseConnectionInfo>();
            Queries = new ObservableCollection<QueryDefinition>();

            // Initialize commands using RelayCommand
            LoadConfigurationCommand = new RelayCommand(ExecuteLoadConfiguration);
            SaveConfigurationCommand = new RelayCommand(ExecuteSaveConfiguration, CanExecuteSaveConfiguration); // Add CanExecute logic if needed
            OpenConnectionManagerCommand = new RelayCommand(ExecuteOpenConnectionManager);
            OpenQueryManagerCommand = new RelayCommand(ExecuteOpenQueryManager);

            // Load configuration immediately on creation (alternative: call from App.xaml.cs)
            // ExecuteLoadConfiguration(null); // Commented out - will call from App.xaml.cs for better control
        }

        // --- Command Execution Methods ---

        private void ExecuteLoadConfiguration(object? parameter)
        {
            _appConfig = _configurationManager.LoadConfiguration();

            // Clear existing collections before loading
            Connections.Clear();
            Queries.Clear();

            // Populate ObservableCollections from loaded configuration
            // Order them for consistent display (optional)
            foreach (var conn in _appConfig.Connections.OrderBy(c => c.ConnectionName))
            {
                Connections.Add(conn);
            }
            foreach (var query in _appConfig.Queries.OrderBy(q => q.Name))
            {
                Queries.Add(query);
            }
            System.Diagnostics.Debug.WriteLine($"Configuration loaded. Connections: {Connections.Count}, Queries: {Queries.Count}");
            System.Diagnostics.Debug.WriteLine($"Config file path: {_configurationManager.GetConfigFilePath()}");
        }

        private void ExecuteSaveConfiguration(object? parameter)
        {
            // Update the _appConfig object from the ObservableCollections before saving
            // This ensures any changes made via the UI (add/remove/edit) are persisted
            _appConfig.Connections = Connections.ToList();
            _appConfig.Queries = Queries.ToList();

            bool success = _configurationManager.SaveConfiguration(_appConfig);

            if (success)
            {
                System.Diagnostics.Debug.WriteLine("Configuration saved successfully.");
                // Optionally show a status message to the user
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Failed to save configuration.");
                // Optionally show an error message to the user
            }
        }

        private bool CanExecuteSaveConfiguration(object? parameter)
        {
            // Example: Only allow saving if there's something to save
            // Or, implement more complex logic based on application state (e.g., IsDirty flag)
            return Connections.Any() || Queries.Any(); // Enable save if there are connections or queries
        }

        private void ExecuteOpenConnectionManager(object? parameter)
        {
            // Create the ViewModel for the Connection Manager, passing existing connections and the service
            var connectionManagerViewModel = new ConnectionManagerViewModel(this.Connections, 
                _databaseService, _encryptionService);

            // Create the View (Window)
            var connectionManagerView = new ConnectionManagerView();

            // Set the DataContext
            connectionManagerView.DataContext = connectionManagerViewModel;

            // **Crucial for Password Handling (Simplified Approach):**
            // We need a way for the ViewModel to potentially access the PasswordBox.
            // Option 1 (Event/Callback - cleaner): ViewModel raises event, View handles it.
            // Option 2 (Pass View reference - tightly coupled): Pass view to VM (not recommended).
            // Option 3 (Pass PasswordBox content via CommandParameter - complex binding):
            // Option 4 (Code-behind helper): ViewModel calls method on View (shown here for simplicity)
            // This requires modifying ConnectionManagerViewModel Save/Test methods slightly.


            // Show the window as a Dialog (waits for it to close)
            // ShowDialog() returns nullable bool? (true if OK/Save, false if Cancel, null if closed otherwise)
            bool? dialogResult = connectionManagerView.ShowDialog();

            // Optional: Check dialog result if needed (e.g., only save if user clicked 'OK')
            // The ConnectionManagerViewModel currently modifies the collection directly.
            // A potentially cleaner approach is for ConnectionManagerViewModel to return the *new* list
            // and MainViewModel decides whether to accept it based on dialogResult.

            // For now, assume changes are reflected in the collection passed by reference.
            // Trigger a save of the potentially modified configuration
            if (SaveConfigurationCommand.CanExecute(null))
            {
                SaveConfigurationCommand.Execute(null);
                Log.Logger?.Information("Configuration automatically saved after closing Connection Manager.");
            }

            // Refresh the list in MainViewModel in case the underlying collection was replaced (if not using ObservableCollection directly)
            // Not strictly necessary here as ConnectionManagerViewModel modifies the ObservableCollection instance passed to it.
        }

        private void ExecuteOpenQueryManager(object? parameter)
        {
            // Pass the shared Queries collection and SqlParser (optional)
            var queryManagerViewModel = new QueryManagerViewModel(
                this.Queries, this.Connections, _databaseService, _sqlValidationService,
                _encryptionService /*, _sqlParser */);

            var queryManagerView = new QueryManagerView
            {
                DataContext = queryManagerViewModel
            };

            queryManagerView.ShowDialog(); // Show as a dialog

            // After the dialog closes, changes made to 'this.Queries' (because it's shared)
            // are ready to be saved.
            if (SaveConfigurationCommand.CanExecute(null))
            {
                SaveConfigurationCommand.Execute(null);
                Log.Logger?.Information("Configuration automatically saved after closing Query Manager.");
            }
        }

        // --- Helper methods (example for later use) ---
        public void AddConnection(DatabaseConnectionInfo newConnection)
        {
            Connections.Add(newConnection);
            // Optionally trigger save or set an IsDirty flag
            // Consider sorting or maintaining order
        }

        public void AddQuery(QueryDefinition newQuery)
        {
            Queries.Add(newQuery);
            // Optionally trigger save or set an IsDirty flag
        }

        // --- Add Helper Method ---
        /// <summary>
        /// Creates or clears the CurrentQueryExecution ViewModel based on
        /// the currently selected Query and Connection.
        /// </summary>
        private void UpdateCurrentQueryExecution()
        {
            if (SelectedQuery != null)
            {
                List<DatabaseConnectionInfo> suitableConnections = new List<DatabaseConnectionInfo>();
                DatabaseConnectionInfo? defaultConnection = null;

                if (SelectedQuery.TargetConnectionIds.Any())
                {
                    // Query specifies target connections
                    suitableConnections.AddRange(
                        Connections.Where(c => SelectedQuery.TargetConnectionIds.Contains(c.Id))
                    );
                    defaultConnection = suitableConnections.FirstOrDefault();
                }
                else
                {
                    // Query does not specify targets, make all configured connections available
                    // Or, you might want a specific global default if no targets specified.
                    suitableConnections.AddRange(Connections);
                    defaultConnection = Connections.FirstOrDefault(); // Default to the first overall connection
                }

                ActiveConnectionForQuery = defaultConnection; // Update the active connection

                if (ActiveConnectionForQuery != null) // Only create execution context if a connection can be determined
                {
                    CurrentQueryExecution = new QueryExecutionViewModel(
                        SelectedQuery,
                        suitableConnections, // Pass the list of suitable connections
                        ActiveConnectionForQuery,   // Pass the default selected one
                        _queryExecutor,
                        _exportService,
                        _databaseService,  // Pass DatabaseService for "Test Connection" in QueryExecutionViewModel
                        _encryptionService
                        );
                }
                else
                {
                    CurrentQueryExecution = null; // No suitable connection found
                    Debug.WriteLine($"No suitable connection found for query '{SelectedQuery.Name}'.");
                }
            }
            else
            {
                CurrentQueryExecution = null; // No query selected
                ActiveConnectionForQuery = null;
            }
        }

    }
}