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
using System.Security.Cryptography;

namespace QueryX.ViewModels
{
    public class ConnectionManagerViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly EncryptionService _encryptionService;

        private DatabaseConnectionInfo? _selectedConnection;
        private DatabaseConnectionInfo? _currentEditConnection; // Connection being edited/added
        private bool _isEditing = false; // Controls enabled state of the form
        private string _statusMessage = string.Empty;
        private bool _isBusy; // Add a backing field for IsBusy

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

        // Add a public property for IsBusy
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value); // Use SetProperty to notify changes
        }
        // public ICommand CloseWindowCommand { get; } // Optional: For explicit close button


        // Constructor
        public ConnectionManagerViewModel(ObservableCollection<DatabaseConnectionInfo> existingConnections, 
            DatabaseService databaseService, EncryptionService encryptionService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
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
                    //Password = connectionToEdit.Password // Copying password reference (handle securely later)
                    EncryptedPassword = connectionToEdit.EncryptedPassword, // Copy encrypted password
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

            string? plainPasswordFromBox = null;
            // --- Get Password from Parameter ---
            if (parameter is System.Windows.Controls.PasswordBox pwdBox) // Check if parameter is a PasswordBox
            {
                // Assign the password just before saving/validation
                //CurrentEditConnection.Password = pwdBox.Password;
                plainPasswordFromBox = pwdBox.Password;
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
            /*
            if (!CurrentEditConnection.UseWindowsAuth && string.IsNullOrWhiteSpace(CurrentEditConnection.UserName))
            {
                StatusMessage = "Error: User Name is required if not using Windows Authentication.";
                return;
            }
            */
            if (!CurrentEditConnection.UseWindowsAuth)
            {
                // If not Windows Auth, a password might be needed or might have been entered
                if (!string.IsNullOrEmpty(plainPasswordFromBox))
                {
                    try
                    {
                        CurrentEditConnection.EncryptedPassword = _encryptionService.EncryptString(plainPasswordFromBox);
                    }
                    catch (CryptographicException) { StatusMessage = "Error: Failed to secure password."; return; }
                }
                else if (CurrentEditConnection.EncryptedPassword == null) // No existing encrypted pass, no new pass entered
                {
                    StatusMessage = "Error: Password is required if not using Windows Authentication.";
                    return;
                }
                // If plainPasswordFromBox is empty but EncryptedPassword exists, we keep the existing one.
                if (string.IsNullOrWhiteSpace(CurrentEditConnection.UserName))
                {
                    StatusMessage = "Error: User Name is required if not using Windows Authentication.";
                    return;
                }
            }
            else // Using Windows Auth
            {
                CurrentEditConnection.EncryptedPassword = null; // Clear any encrypted password
                CurrentEditConnection.UserName = null; // Often good to clear username too
            }

            // Clear any transient decrypted password (should not be set here anyway)
            CurrentEditConnection.DecryptedPasswordForCurrentOperation = null;

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

            string? plainPasswordFromBox = null;
            // --- Get Password from Parameter ---
            if (parameter is System.Windows.Controls.PasswordBox pwdBox)
            {
                // Assign the password just before testing
                plainPasswordFromBox = pwdBox.Password;
            }

            // We'll use the one from the PasswordBox if provided, otherwise try to decrypt the stored one.
            string? passwordForTest = null;
            if (!CurrentEditConnection.UseWindowsAuth)
            {
                if (!string.IsNullOrEmpty(plainPasswordFromBox))
                {
                    passwordForTest = plainPasswordFromBox;
                }
                else if (CurrentEditConnection.EncryptedPassword != null)
                {
                    try
                    {
                        passwordForTest = _encryptionService.DecryptToString(CurrentEditConnection.EncryptedPassword);
                    }
                    catch (CryptographicException) { StatusMessage = "Error: Failed to read stored password for test."; IsBusy = false; return; }

                    if (passwordForTest == null) // Decryption failed or returned null
                    {
                        StatusMessage = "Error: Could not decrypt stored password for testing. Please re-enter.";
                        IsBusy = false;
                        return;
                    }
                }
                else // No password in box, no encrypted password stored, but needed
                {
                    StatusMessage = "Error: Password required for testing this connection type.";
                    IsBusy = false;
                    return;
                }
            }

            StatusMessage = "Testing connection...";
            IsBusy = true;

            // Use a temporary copy or set DecryptedPasswordForCurrentOperation carefully
            var tempConnectionInfoForTest = new DatabaseConnectionInfo // Create a temp DTO for testing
            {
                // Copy all relevant properties from CurrentEditConnection
                DbType = CurrentEditConnection.DbType,
                Server = CurrentEditConnection.Server,
                DatabaseName = CurrentEditConnection.DatabaseName,
                UseWindowsAuth = CurrentEditConnection.UseWindowsAuth,
                UserName = CurrentEditConnection.UseWindowsAuth ? null : CurrentEditConnection.UserName,
                DecryptedPasswordForCurrentOperation = passwordForTest // Use the resolved plain text password
            };

            var (isSuccess, message) = await _databaseService.TestConnectionAsync(tempConnectionInfoForTest);
            StatusMessage = message; // Display result from DatabaseService
            IsBusy = false;

            // Optionally show a MessageBox as well
            // MessageBox.Show(message, "Connection Test Result", MessageBoxButton.OK,
            //                 isSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
    }
}