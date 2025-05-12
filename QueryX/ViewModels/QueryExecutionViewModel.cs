using QueryX.Models;
using QueryX.Services;
using QueryX.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Threading; // For CancellationTokenSource
using System.Threading.Tasks; // For Task
using System.Data; // For DataTable check
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32; // For SaveFileDialog

namespace QueryX.ViewModels // Ensure namespace matches
{
    public class QueryExecutionViewModel : ViewModelBase
    {
        private readonly QueryDefinition _queryDefinition;
        //private readonly DatabaseConnectionInfo _connectionInfo;
        private readonly QueryExecutor _queryExecutor;
        private readonly ExportService _exportService;
        private readonly DatabaseService _databaseService;
        private readonly EncryptionService _encryptionService;

        private CancellationTokenSource? _cancellationTokenSource; // To allow cancellation

        // --- NEW Properties for Connection Selection ---
        public ObservableCollection<DatabaseConnectionInfo> AvailableConnectionsForExecution { get; }
        private DatabaseConnectionInfo? _selectedConnectionForExecution;
        private string _connectionTestStatusMessage = "Untested";
        public DatabaseConnectionInfo? SelectedConnectionForExecution
        {
            get => _selectedConnectionForExecution;
            set
            {
                if (SetProperty(ref _selectedConnectionForExecution, value))
                {
                    ConnectionTestStatusMessage = "Untested";
                    if (value != null)
                    {
                        StatusMessage = $"Using '{value.ConnectionName}'. Parameters loaded.";
                    }
                    else if (AvailableConnectionsForExecution.Any())
                    {
                        StatusMessage = "Please select a database connection for this query.";
                    }
                    else
                    {
                        StatusMessage = "No suitable database connection available for this query.";
                    }

                    // Re-validate CanExecute for ExecuteQueryCommand if it depends on a connection being selected
                    ((RelayCommand)ExecuteQueryCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)TestSelectedConnectionCommand).RaiseCanExecuteChanged();
                }
            }
        }
        public string ConnectionTestStatusMessage
        {
            get => _connectionTestStatusMessage;
            private set => SetProperty(ref _connectionTestStatusMessage, value);
        }

        private ObservableCollection<ParameterInputViewModel> _parameters = new ObservableCollection<ParameterInputViewModel>();
        private QueryResult? _currentResult;
        private bool _isBusy = false;
        private string _statusMessage = string.Empty;

        // The query definition this ViewModel handles
        public QueryDefinition TheQuery => _queryDefinition;

        // Collection of parameter input ViewModels for the UI
        public ObservableCollection<ParameterInputViewModel> Parameters
        {
            get => _parameters;
            private set => SetProperty(ref _parameters, value);
        }

        // Holds the result of the last execution
        public QueryResult? CurrentResult
        {
            get => _currentResult;
            private set
            {
                if (SetProperty(ref _currentResult, value))
                {
                    // When results change, update CanExecute for export commands
                    ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ExportExcelCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // Indicates if a query is currently running
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    // Re-evaluate CanExecute for ALL relevant commands
                    ((RelayCommand)ExecuteQueryCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)CancelQueryCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ExportCsvCommand).RaiseCanExecuteChanged(); // <-- Add Export
                    ((RelayCommand)ExportExcelCommand).RaiseCanExecuteChanged(); // <-- Add Export
                }
            }
        }

        // Status message for the user
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        // Commands
        public ICommand ExecuteQueryCommand { get; }
        public ICommand CancelQueryCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand TestSelectedConnectionCommand { get; }

        // Constructor
        public QueryExecutionViewModel(QueryDefinition queryDefinition, 
            IEnumerable<DatabaseConnectionInfo> availableConnections, 
            DatabaseConnectionInfo? initialConnection, QueryExecutor queryExecutor, 
            ExportService exportService, DatabaseService databaseService,
            EncryptionService encryptionService)
        {
            _queryDefinition = queryDefinition ?? throw new ArgumentNullException(nameof(queryDefinition));
            //_connectionInfo = connectionInfo ?? throw new ArgumentNullException(nameof(connectionInfo));
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));

            ExecuteQueryCommand = new RelayCommand(async (p) => await ExecuteQueryAsync(), CanExecuteQuery);
            CancelQueryCommand = new RelayCommand(ExecuteCancelQuery, CanCancelQuery);
            ExportCsvCommand = new RelayCommand(async (p) => await ExecuteExportCsvAsync(), CanExecuteExport);
            ExportExcelCommand = new RelayCommand(async (p) => await ExecuteExportExcelAsync(), CanExecuteExport);
            TestSelectedConnectionCommand = new RelayCommand(async (p) => await ExecuteTestSelectedConnectionAsync(), CanTestSelectedConnection);

            // Initialize connection properties after Command setup
            AvailableConnectionsForExecution = new ObservableCollection<DatabaseConnectionInfo>(availableConnections ?? new List<DatabaseConnectionInfo>());
            SelectedConnectionForExecution = initialConnection; // Set the initial selected connection

            LoadParameters();
            if (SelectedConnectionForExecution == null && AvailableConnectionsForExecution.Any())
            {
                StatusMessage = "Please select a database connection for this query.";
            }
            else if (SelectedConnectionForExecution != null)
            {
                StatusMessage = $"Using '{SelectedConnectionForExecution.ConnectionName}'. Enter parameters and click Execute.";
            }
            else
            {
                StatusMessage = "No suitable database connection available for this query.";
            }
        }

        // Populates the Parameters collection based on the QueryDefinition
        private void LoadParameters()
        {
            Parameters.Clear();
            foreach (var paramDef in _queryDefinition.Parameters.OrderBy(p => p.DisplayName)) // Order alphabetically for UI
            {
                Parameters.Add(new ParameterInputViewModel(paramDef));
            }
        }

        // --- Command Implementations ---

        private bool CanExecuteQuery(object? parameter)
        {
            return !IsBusy && SelectedConnectionForExecution != null; // <-- Check if a connection is selected
        }

        private async Task ExecuteQueryAsync()
        {
            if (!CanExecuteQuery(null) || SelectedConnectionForExecution == null) // Check SelectedConnectionForExecution
            {
                StatusMessage = "Cannot execute: No database connection selected.";
                return;
            }

            // 1. Validate Parameters
            var validationErrors = new List<string>();
            var parameterValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var paramVM in Parameters)
            {
                if (!paramVM.IsValid(out string? error))
                {
                    if (error != null) validationErrors.Add(error);
                }
                // Add value to dictionary using placeholder name *without* prefix (@, :)
                parameterValues[paramVM.Definition.PlaceholderName.TrimStart('@', ':')] = paramVM.Value;
            }

            if (validationErrors.Any())
            {
                StatusMessage = "Validation Error(s): " + string.Join("; ", validationErrors);
                CurrentResult = new QueryResult(StatusMessage); // Show error in result status
                return;
            }

            // 2. Execute Query
            IsBusy = true;
            StatusMessage = $"Executing query '{_queryDefinition.Name}' on '{SelectedConnectionForExecution.ConnectionName}'...";
            CurrentResult = null; // Clear previous results
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                CurrentResult = await _queryExecutor.ExecuteAsync(
                    _queryDefinition,
                    SelectedConnectionForExecution,
                    parameterValues,
                    _cancellationTokenSource.Token);

                // Update status based on result
                if (CurrentResult.IsSuccess)
                {
                    if (CurrentResult.ResultTable != null)
                    {
                        StatusMessage = $"Query executed successfully. {CurrentResult.ResultTable.Rows.Count} rows returned in {CurrentResult.Duration?.TotalSeconds:F2}s.";
                    }
                    else
                    {
                        StatusMessage = $"Query executed successfully. {CurrentResult.RecordsAffected ?? 0} records affected in {CurrentResult.Duration?.TotalSeconds:F2}s.";
                    }
                }
                else
                {
                    StatusMessage = $"Error: {CurrentResult.ErrorMessage}";
                }
            }
            catch (Exception ex) // Catch unexpected errors from the executor call itself
            {
                StatusMessage = $"Execution failed: {ex.Message}";
                CurrentResult = new QueryResult(StatusMessage);
            }
            finally
            {
                IsBusy = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private bool CanCancelQuery(object? parameter)
        {
            return IsBusy; // Can cancel only if busy
        }

        private void ExecuteCancelQuery(object? parameter)
        {
            if (CanCancelQuery(null) && _cancellationTokenSource != null)
            {
                StatusMessage = "Cancelling query execution...";
                _cancellationTokenSource.Cancel();
            }
        }

        private bool CanExecuteExport(object? parameter)
        {
            // Can export if not busy and there is a result table with rows
            return !IsBusy && CurrentResult?.ResultTable != null && CurrentResult.ResultTable.Rows.Count > 0;
        }

        private async Task ExecuteExportCsvAsync()
        {
            if (!CanExecuteExport(null) || CurrentResult?.ResultTable == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"{_queryDefinition.Name}_Results_{DateTime.Now:yyyyMMddHHmmss}.csv", // Default filename
                Title = "Save Results as CSV"
            };

            if (sfd.ShowDialog() == true)
            {
                IsBusy = true; // Show busy indicator during export
                StatusMessage = $"Exporting to CSV: {Path.GetFileName(sfd.FileName)}...";
                try
                {
                    await _exportService.ExportDataTableToCsvAsync(CurrentResult.ResultTable, sfd.FileName);
                    StatusMessage = $"Successfully exported results to CSV: {sfd.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error exporting to CSV: {ex.Message}";
                    // Optionally show error in MessageBox
                    // MessageBox.Show($"Failed to export to CSV:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ExecuteExportExcelAsync()
        {
            if (!CanExecuteExport(null) || CurrentResult?.ResultTable == null) return;

            var sfd = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                FileName = $"{_queryDefinition.Name}_Results_{DateTime.Now:yyyyMMddHHmmss}.xlsx", // Default filename
                Title = "Save Results as Excel"
            };

            if (sfd.ShowDialog() == true)
            {
                IsBusy = true; // Show busy indicator during export
                StatusMessage = $"Exporting to Excel: {Path.GetFileName(sfd.FileName)}...";
                try
                {
                    await _exportService.ExportDataTableToExcelAsync(CurrentResult.ResultTable, sfd.FileName);
                    StatusMessage = $"Successfully exported results to Excel: {sfd.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error exporting to Excel: {ex.Message}";
                    // Optionally show error in MessageBox
                    // MessageBox.Show($"Failed to export to Excel:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
        
        // --- NEW Command Implementation for Testing Selected Connection ---
        private bool CanTestSelectedConnection(object? parameter)
        {
            return !IsBusy && SelectedConnectionForExecution != null;
        }

        private async Task ExecuteTestSelectedConnectionAsync()
        {
            if (!CanTestSelectedConnection(null) || SelectedConnectionForExecution == null) return;

            IsBusy = true;
            string originalStatus = StatusMessage; // Store original status
            StatusMessage = $"Testing connection to '{SelectedConnectionForExecution.ConnectionName}'...";
            ConnectionTestStatusMessage = "Testing...";

            // --- Temporary Decryption Logic for Testing ---
            bool passwordPreparationOk = true;
            if (!SelectedConnectionForExecution.UseWindowsAuth)
            {
                if (SelectedConnectionForExecution.EncryptedPassword != null)
                {
                    try
                    {
                        SelectedConnectionForExecution.DecryptedPasswordForCurrentOperation =
                            _encryptionService.DecryptToString(SelectedConnectionForExecution.EncryptedPassword);

                        if (SelectedConnectionForExecution.DecryptedPasswordForCurrentOperation == null)
                        {
                            // Decryption failed (e.g., different user/machine or corrupted data)
                            ConnectionTestStatusMessage = "Failed: Decrypt error";
                            StatusMessage = $"Test for '{SelectedConnectionForExecution.ConnectionName}': Failed to decrypt stored password.";
                            passwordPreparationOk = false;
                        }
                    }
                    catch (CryptographicException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Test Connection: Password decryption failed: {ex.Message}");
                        ConnectionTestStatusMessage = "Failed: Decrypt error";
                        StatusMessage = $"Test for '{SelectedConnectionForExecution.ConnectionName}': Password decryption error.";
                        passwordPreparationOk = false;
                    }
                }
                else // Not Windows Auth, but no encrypted password is stored.
                {
                    // This connection requires a password, but none is configured.
                    // The BuildConnectionString in DatabaseService will likely fail or use an empty password.
                    // No explicit error here, let TestConnectionAsync report the connection failure.
                    // For clarity, we could set a message, but TestConnectionAsync will give more specific DB errors.
                    System.Diagnostics.Debug.WriteLine($"Test Connection: No encrypted password for '{SelectedConnectionForExecution.ConnectionName}' which requires password auth.");
                }
            }
            var (isSuccess, message) = (false, "Password preparation failed."); // Default to failure

            if (passwordPreparationOk)
            {
                (isSuccess, message) = await _databaseService.TestConnectionAsync(SelectedConnectionForExecution);
                ConnectionTestStatusMessage = isSuccess ? "OK" : $"Failed: {message.Split('\n')[0]}"; // Concise error
            }

            StatusMessage = message; // Display test result

            // You might want to revert to originalStatus after a few seconds or if successful
            // For now, it just shows the test result.
            // Consider a timed revert:
            // await Task.Delay(3000);
            // if (StatusMessage == message) StatusMessage = originalStatus; // Revert if status hasn't changed further
            
            // --- Clear Decrypted Password ---
            if (SelectedConnectionForExecution != null)
            {
                SelectedConnectionForExecution.DecryptedPasswordForCurrentOperation = null;
            }

            IsBusy = false;
        }
    }
}