using QueryX.Models;
using QueryX.Services;
using QueryX.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Diagnostics; // For async Task

namespace QueryX.ViewModels
{
    public class ConnectionManagerViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private DatabaseConnectionInfo? _selectedConnection;
        private DatabaseConnectionInfo? _currentEditConnection; // Connection being edited/added
        private bool _isEditing = false; // Controls enabled state of the form
        private string _statusMessage = string.Empty;

        // List of connections displayed in the ListBox
        public ObservableCollection<DatabaseConnectionInfo> Connections { get; private set; }

        // Holds the connection selected in the ListBox
        public DatabaseConnectionInfo? SelectedConnection
        {
            get => _selectedConnection;
            set
            {
                if (SetProperty(ref _selectedConnection, value))
                {
                    // When selection changes, trigger edit (or view details)
                    if (value != null)
                    {
                        EditCommand.Execute(value); // Start editing the selected item
                    }
                    else
                    {
                        CancelEditCommand.Execute(null); // Clear form if selection is cleared
                    }
                     // Raise CanExecuteChanged for commands that depend on selection (like Remove)
                     ((RelayCommand)RemoveCommand).RaiseCanExecuteChanged(); // Need to cast or use a specific RelayCommand type
                     // Or if SaveCommand depends on selection too:
                     // ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // Represents the connection currently bound to the form fields
        public DatabaseConnectionInfo? CurrentEditConnection
        {
            get => _currentEditConnection;
            private set // Private setter, modified internally or via commands
            {
                // SetProperty handles INotifyPropertyChanged for the object itself
                // but we need to manually raise changes for individual properties bound to the form
                if (SetProperty(ref _currentEditConnection, value))
                {
                    IsEditing = value != null; // Enable/disable form based on whether we are editing
                    // Manually raise property changed for all bound properties when the whole object changes
                    OnPropertyChanged(nameof(ConnectionName));
                    OnPropertyChanged(nameof(SelectedDbType));
                    OnPropertyChanged(nameof(Server));
                    OnPropertyChanged(nameof(DatabaseName));
                    OnPropertyChanged(nameof(UseWindowsAuth));
                    OnPropertyChanged(nameof(UserName));
                    // Password needs special handling - not directly bound here
                }
            }
        }

        // Flag to enable/disable the editing form group box
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        // Status message display
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }


        #region Form Binding Properties (Proxy to CurrentEditConnection)

        // Expose properties of CurrentEditConnection for binding, handling nulls
        public string ConnectionName
        {
            get => CurrentEditConnection?.ConnectionName ?? string.Empty;
            set
            {
                if (CurrentEditConnection != null && CurrentEditConnection.ConnectionName != value)
                {
                    CurrentEditConnection.ConnectionName = value;
                    OnPropertyChanged(); // Notify UI this property changed
                }
            }
        }

        // Provide list of enum values for ComboBox
        public IEnumerable<DatabaseType> AvailableDbTypes => Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>();

        public DatabaseType SelectedDbType
        {
            get => CurrentEditConnection?.DbType ?? DatabaseType.SQLServer; // Default if null
            set
            {
                if (CurrentEditConnection != null && CurrentEditConnection.DbType != value)
                {
                    CurrentEditConnection.DbType = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Server
        {
            get => CurrentEditConnection?.Server ?? string.Empty;
            set
            {
                if (CurrentEditConnection != null && CurrentEditConnection.Server != value)
                {
                    CurrentEditConnection.Server = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DatabaseName
        {
            get => CurrentEditConnection?.DatabaseName ?? string.Empty;
            set
            {
                if (CurrentEditConnection != null && CurrentEditConnection.DatabaseName != value)
                {
                    CurrentEditConnection.DatabaseName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseWindowsAuth
        {
            get => CurrentEditConnection?.UseWindowsAuth ?? false;
            set
            {
                if (CurrentEditConnection != null && CurrentEditConnection.UseWindowsAuth != value)
                {
                    CurrentEditConnection.UseWindowsAuth = value;
                    OnPropertyChanged();
                    // When UseWindowsAuth changes, also notify that IsEnabled state for User/Pass might change
                    OnPropertyChanged(nameof(UserName));
                    // Password needs update too
                }
            }
        }

        public string? UserName // Nullable string
        {
            get => CurrentEditConnection?.UserName;
            set
            {
                if (CurrentEditConnection != null && CurrentEditConnection.UserName != value)
                {
                    CurrentEditConnection.UserName = value;
                    OnPropertyChanged();
                }
            }
        }

        // Password is not directly bound for security. Will be retrieved from PasswordBox when needed (e.g., Save, Test).

        #endregion


        // --- Commands ---
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; } // Not typically invoked by button, but by selecting item
        public ICommand RemoveCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand TestCommand { get; }
        // public ICommand CloseWindowCommand { get; } // Optional: For explicit close button


        // Constructor
        public ConnectionManagerViewModel(ObservableCollection<DatabaseConnectionInfo> existingConnections, DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            Connections = existingConnections;

            // Initialize Commands
            AddCommand = new RelayCommand(ExecuteAdd);
            EditCommand = new RelayCommand(ExecuteEdit, CanExecuteEdit); // Parameter is the item to edit
            RemoveCommand = new RelayCommand(ExecuteRemove, CanExecuteRemove);
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CancelEditCommand = new RelayCommand(ExecuteCancelEdit);
            TestCommand = new RelayCommand(async (p) => await ExecuteTestConnectionAsync(p), CanExecuteTestConnection); // Async command

            // Initial state: No item selected, form disabled
            IsEditing = false;
        }


        // --- Command Implementations ---

        private void ExecuteAdd(object? parameter)
        {
            StatusMessage = string.Empty;
            // Create a new blank connection object for editing
            CurrentEditConnection = new DatabaseConnectionInfo { Id = Guid.NewGuid(), ConnectionName = "New Connection" };
            IsEditing = true; // Enable the form
            SelectedConnection = null; // Deselect any item in the list
        }

        private bool CanExecuteEdit(object? parameter) => parameter is DatabaseConnectionInfo;
        private void ExecuteEdit(object? parameter)
        {
            StatusMessage = string.Empty;
            if (parameter is DatabaseConnectionInfo connectionToEdit)
            {
                // Create a *copy* of the selected connection for editing
                // This prevents changes from directly affecting the list until Save is clicked
                CurrentEditConnection = new DatabaseConnectionInfo
                {
                    Id = connectionToEdit.Id,
                    ConnectionName = connectionToEdit.ConnectionName,
                    DbType = connectionToEdit.DbType,
                    Server = connectionToEdit.Server,
                    DatabaseName = connectionToEdit.DatabaseName,
                    UseWindowsAuth = connectionToEdit.UseWindowsAuth,
                    UserName = connectionToEdit.UserName,
                    Password = connectionToEdit.Password // Copying password reference (handle securely later)
                };
                IsEditing = true;
            }
        }


        private bool CanExecuteRemove(object? parameter) => SelectedConnection != null; // Can only remove if an item is selected
        private void ExecuteRemove(object? parameter)
        {
            if (SelectedConnection != null)
            {
                // Confirmation dialog is good practice here!
                // MessageBoxResult result = MessageBox.Show($"Are you sure you want to remove '{SelectedConnection.ConnectionName}'?",
                //                                        "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                // if (result == MessageBoxResult.Yes)
                // {
                Connections.Remove(SelectedConnection);
                CurrentEditConnection = null; // Clear edit form
                StatusMessage = "Connection removed.";
                // }
            }
        }


        private bool CanExecuteSave(object? parameter) => CurrentEditConnection != null && IsEditing; // Can save if editing an object
        private void ExecuteSave(object? parameter)
        {
            if (CurrentEditConnection == null) return;

            // --- Get Password from Parameter ---
            if (parameter is System.Windows.Controls.PasswordBox pwdBox) // Check if parameter is a PasswordBox
            {
                // Assign the password just before saving/validation
                CurrentEditConnection.Password = pwdBox.Password;
            }
            else
            {
                // Handle case where parameter is not passed correctly (optional, for robustness)
                // Maybe fallback to existing password if editing? For adding, it should be there.
                // For now, we assume it's passed correctly if needed.
                if (!CurrentEditConnection.UseWindowsAuth && string.IsNullOrEmpty(CurrentEditConnection.Password))
                {
                    // If password wasn't retrieved and is needed, show error or handle
                    System.Diagnostics.Debug.WriteLine("Warning: PasswordBox parameter was not received correctly in ExecuteSave.");
                }
            }

            // Basic Validation (Example)
            if (string.IsNullOrWhiteSpace(CurrentEditConnection.ConnectionName))
            {
                StatusMessage = "Error: Connection Name cannot be empty.";
                return;
            }
            if (string.IsNullOrWhiteSpace(CurrentEditConnection.Server))
            {
                StatusMessage = "Error: Server/Host/File Path cannot be empty.";
                return;
            }
            if (!CurrentEditConnection.UseWindowsAuth && string.IsNullOrWhiteSpace(CurrentEditConnection.UserName))
            {
                StatusMessage = "Error: User Name is required if not using Windows Authentication.";
                return;
            }
            // Check if it's a new connection (by ID) or an existing one being updated
            var existing = Connections.FirstOrDefault(c => c.Id == CurrentEditConnection.Id);
            if (existing != null)
            {
                // Update existing connection in the list (replace item or update properties)
                int index = Connections.IndexOf(existing);
                if (index != -1) Connections[index] = CurrentEditConnection; // Replace item to trigger list update
            }
            else
            {
                // Add new connection to the list
                Connections.Add(CurrentEditConnection);
            }
            StatusMessage = $"Connection change saved.";
            CurrentEditConnection = null; // Clear the form, disable editing
            IsEditing = false;
        }


        private void ExecuteCancelEdit(object? parameter)
        {
            CurrentEditConnection = null; // Clear the form
            IsEditing = false;
            StatusMessage = "Edit cancelled.";
        }


        private bool CanExecuteTestConnection(object? parameter) => CurrentEditConnection != null && IsEditing;
        private async Task ExecuteTestConnectionAsync(object? parameter)
        {
            if (CurrentEditConnection == null) return;

            // --- Get Password from Parameter ---
            if (parameter is System.Windows.Controls.PasswordBox pwdBox)
            {
                // Assign the password just before testing
                CurrentEditConnection.Password = pwdBox.Password;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Warning: PasswordBox parameter was not received correctly in ExecuteTestConnectionAsync.");
            }

            StatusMessage = "Testing connection...";
            var (isSuccess, message) = await _databaseService.TestConnectionAsync(CurrentEditConnection);
            StatusMessage = message; // Display result from DatabaseService

            // Optionally show a MessageBox as well
            // MessageBox.Show(message, "Connection Test Result", MessageBoxButton.OK,
            //                 isSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
    }
}