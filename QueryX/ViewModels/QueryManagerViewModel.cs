using QueryX.Models;
using QueryX.Helpers; // For RelayCommand
using QueryX.Services; // For SqlParser (optional future use)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows; // For MessageBox
using System.Text;

namespace QueryX.ViewModels
{
    public class QueryManagerViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly SqlValidationService _sqlValidationService;
        private readonly EncryptionService _encryptionService;


        private QueryDefinition? _selectedQueryInList;
        public QueryDefinition? SelectedQueryInList // Bound to the ListBox of defined queries
        {
            get => _selectedQueryInList;
            set
            {
                if (_selectedQueryInList != value)
                {
                    // Before changing selection, prompt to save if there are pending changes to EditingQueryCopy
                    // For simplicity, we'll skip prompting for now, changes are lost if not saved.
                    // A "IsDirty" flag on EditingQueryCopy would be useful here.

                    SetProperty(ref _selectedQueryInList, value);
                    _isNewQueryMode = false; // Reset new query mode
                    if (_selectedQueryInList != null)
                    {
                        LoadQueryForEditing(_selectedQueryInList);
                        StatusMessage = $"Editing '{_selectedQueryInList.Name}'.";
                    }
                    else
                    {
                        EditingQueryCopy = null; // Clear the form
                        StatusMessage = "Select a query or add a new one.";
                    }
                    ((RelayCommand)DeleteSelectedQueryCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private QueryDefinition? _editingQueryCopy; // A copy of the selected query for editing
        public QueryDefinition? EditingQueryCopy // The object bound to the form fields
        {
            get => _editingQueryCopy;
            set
            {
                if (SetProperty(ref _editingQueryCopy, value))
                {
                    PopulateAvailableConnectionsForTargeting(_editingQueryCopy);
                    SelectedParameterForEditing = _editingQueryCopy?.Parameters.FirstOrDefault();
                    // Raise CanExecute for commands that depend on EditingQueryCopy
                    ((RelayCommand)SaveQueryCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RevertQueryCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)AddSqlTemplateCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RemoveLastSqlTemplateCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)AddParameterCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RemoveSelectedParameterCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isNewQueryMode = false; // Flag to indicate if we are editing a new unsaved query

        
        private ParameterDefinition? _selectedParameterForEditing; // For DataGrid selection within EditingQueryCopy
        public ParameterDefinition? SelectedParameterForEditing // For DataGrid inside EditingQueryCopy
        {
            get => _selectedParameterForEditing;
            set
            {
                if (SetProperty(ref _selectedParameterForEditing, value))
                {
                    ((RelayCommand)RemoveSelectedParameterCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private DatabaseConnectionInfo? _validationConnection;
        public DatabaseConnectionInfo? ValidationConnection
        {
            get => _validationConnection;
            set => SetProperty(ref _validationConnection, value);
        }

        public ObservableCollection<DatabaseConnectionInfo> ValidationConnections { get; } = new();

        private readonly ObservableCollection<DatabaseConnectionInfo> _allConfiguredConnections;
        public ObservableCollection<SelectableConnectionViewModel> AvailableConnectionsForTargeting { get; } = new ObservableCollection<SelectableConnectionViewModel>();

        // private readonly SqlParser _sqlParser; // Optional for future validation

        // The shared collection of all query definitions from MainViewModel
        public ObservableCollection<QueryDefinition> Queries { get; }

        // For ComboBox in DataGrid - list of ParameterDataType enum values
        //public IEnumerable<ParameterDataType> ParameterDataTypes => Enum.GetValues(typeof(ParameterDataType)).Cast<ParameterDataType>();
        public ParameterDataType[] ParameterDataTypes => Enum.GetValues(typeof(ParameterDataType)).Cast<ParameterDataType>().ToArray(); // NEW: Convert to array

        // Commands
        public ICommand AddNewQueryCommand { get; }
        public ICommand DeleteSelectedQueryCommand { get; }
        public ICommand SaveQueryCommand { get; }
        public ICommand RevertQueryCommand { get; }
        public ICommand AddSqlTemplateCommand { get; }
        public ICommand RemoveLastSqlTemplateCommand { get; }
        public ICommand AddParameterCommand { get; }
        public ICommand RemoveSelectedParameterCommand { get; }
        public ICommand CheckSyntaxCommand { get; }
        public ICommand CloseWindowCommand { get; } // To close the window

        // Constructor
        public QueryManagerViewModel(ObservableCollection<QueryDefinition> queries,
            ObservableCollection<DatabaseConnectionInfo> allConfiguredConnections,
            DatabaseService databaseService, SqlValidationService sqlValidationService,
            EncryptionService encryptionService /*, SqlParser sqlParser - optional */)
        {
            Queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _allConfiguredConnections = allConfiguredConnections ?? throw new ArgumentNullException(nameof(allConfiguredConnections));

            _databaseService = databaseService;
            _sqlValidationService = sqlValidationService;
            _encryptionService = encryptionService;
            // _sqlParser = sqlParser; // Optional

            AddNewQueryCommand = new RelayCommand(ExecuteAddNewQuery);
            DeleteSelectedQueryCommand = new RelayCommand(ExecuteDeleteSelectedQuery, CanExecuteDeleteSelectedQuery);

            SaveQueryCommand = new RelayCommand(ExecuteSaveQuery, CanExecuteSaveQuery);
            RevertQueryCommand = new RelayCommand(ExecuteRevertQuery, CanExecuteRevertQuery);

            AddSqlTemplateCommand = new RelayCommand(ExecuteAddSqlTemplate, CanExecuteSelectedQueryCommands);
            RemoveLastSqlTemplateCommand = new RelayCommand(ExecuteRemoveLastSqlTemplate, CanExecuteRemoveLastSqlTemplate);

            AddParameterCommand = new RelayCommand(ExecuteAddParameter, CanExecuteSelectedQueryCommands);
            RemoveSelectedParameterCommand = new RelayCommand(ExecuteRemoveSelectedParameter, CanExecuteRemoveSelectedParameter);

            CheckSyntaxCommand = new RelayCommand<SqlTemplateEditable>(async (template) => await ExecuteCheckSyntaxAsync(template), (template) => EditingQueryCopy != null && template != null && ValidationConnection != null);

            // Command to close the window (needs to be passed the window instance or use a messaging system)
            // For simplicity, we'll assume the View handles its closure via the IsCancel button for now,
            // but a command is good practice for more complex scenarios.
            // This CloseWindowCommand is more for if we had a ViewModel-driven close button.
            CloseWindowCommand = new RelayCommand(p => (p as Window)?.Close(), p => p is Window);

            if (!Queries.Any())
            {
                StatusMessage = "No queries defined. Click 'Add New Query' to start.";
            }
            else
            {
                StatusMessage = "Select a query to edit or add a new one.";
            }
        }

        private void PopulateAvailableConnectionsForTargeting(QueryDefinition query)
        {
            AvailableConnectionsForTargeting.Clear();
            ValidationConnections.Clear();
            if (query == null) return;

            foreach (var globalConn in _allConfiguredConnections.OrderBy(c => c.ConnectionName))
            {
                var selectableVM = new SelectableConnectionViewModel(
                    globalConn.Id,
                    globalConn.ConnectionName,
                    query.TargetConnectionIds.Contains(globalConn.Id)
                );
                // Subscribe to IsSelected changes to update the model directly
                selectableVM.PropertyChanged += (s, e) =>
                {
                    // This direct update is complex due to copy. Simpler to update on Save.
                    // For now, we'll rely on the Save command to gather these selections.
                    // If IsDirty flag is implemented, this would set it.
                };
                AvailableConnectionsForTargeting.Add(selectableVM);

                // Populate validation connections from the ones checked by the user
                UpdateValidationConnections();
            }
        }

        // Call this when target connections change
        private void UpdateValidationConnections()
        {
            ValidationConnections.Clear();
            var currentlySelectedValidationConnection = ValidationConnection;

            if (EditingQueryCopy != null && EditingQueryCopy.TargetConnectionIds.Any())
            {
                // The query has specific target connections defined; use those for validation options.
                foreach (var connVM in AvailableConnectionsForTargeting.Where(c => c.IsSelected))
                {
                    var fullConnInfo = _allConfiguredConnections.FirstOrDefault(c => c.Id == connVM.ConnectionId);
                    if (fullConnInfo != null) ValidationConnections.Add(fullConnInfo);
                }
            } 
            else
            {
                // No specific target connections defined for the query, so offer all configured connections for validation.
                foreach (var fullConnInfo in _allConfiguredConnections.OrderBy(c => c.ConnectionName))
                {
                    ValidationConnections.Add(fullConnInfo);
                }
            }

            // Try to re-select the previously selected validation connection
            ValidationConnection = ValidationConnections.FirstOrDefault(c => c.Id == currentlySelectedValidationConnection?.Id) ?? ValidationConnections.FirstOrDefault();
        }


        private bool CanExecuteSaveQuery(object? parameter) => EditingQueryCopy != null;
        private bool CanExecuteRevertQuery(object? parameter) => EditingQueryCopy != null;
        private bool CanExecuteSelectedQueryCommands(object? parameter) => EditingQueryCopy != null;
        private bool CanExecuteRemoveLastSqlTemplate(object? parameter) => EditingQueryCopy?.SqlTemplates.Any() ?? false;
        private bool CanExecuteDeleteSelectedQuery(object? parameter) => SelectedQueryInList != null && !_isNewQueryMode;
        private bool CanExecuteRemoveSelectedParameter(object? parameter) => EditingQueryCopy != null && SelectedParameterForEditing != null;
        

        private void LoadQueryForEditing(QueryDefinition? queryToEdit)
        {
            if (queryToEdit == null)
            {
                EditingQueryCopy = null;
                return;
            }
            // Create a deep copy for editing
            EditingQueryCopy = new QueryDefinition
            {
                Id = queryToEdit.Id,
                Name = queryToEdit.Name,
                Description = queryToEdit.Description,
                FolderPath = queryToEdit.FolderPath,
                SqlTemplates = new ObservableCollection<SqlTemplateEditable>(queryToEdit.SqlTemplates.Select(st => new SqlTemplateEditable(st.SqlText))),
                Parameters = new ObservableCollection<ParameterDefinition>(queryToEdit.Parameters.Select(p => new ParameterDefinition // Simple shallow copy for ParameterDefinition for now
                {
                    PlaceholderName = p.PlaceholderName,
                    DisplayName = p.DisplayName,
                    DataType = p.DataType,
                    IsRequired = p.IsRequired,
                    DefaultValue = p.DefaultValue,
                    Tooltip = p.Tooltip,
                    ValueListOptions = p.ValueListOptions != null ? new List<string>(p.ValueListOptions) : new List<string>()
                })),
                TargetConnectionIds = new List<Guid>(queryToEdit.TargetConnectionIds)
            };
            _isNewQueryMode = false;
        }

        private void ExecuteAddNewQuery(object? parameter)
        {
            var newQuery = new QueryDefinition
            {
                Id = Guid.NewGuid(),
                Name = "New Query",
                Description = "Describe your new query here.",
                SqlTemplates = new ObservableCollection<SqlTemplateEditable>
                {
                    new SqlTemplateEditable("-- SQL --")
                },
                Parameters = new ObservableCollection<ParameterDefinition>(), // Initialize as empty
                TargetConnectionIds = new List<Guid>() // Initialize as empty
            };

            EditingQueryCopy = newQuery;

            _isNewQueryMode = true;
            SelectedQueryInList = null; // Deselect from the main list as we're editing a new, unsaved one
            StatusMessage = "Editing new query. Click Save to add it to the list.";
            ((RelayCommand)DeleteSelectedQueryCommand).RaiseCanExecuteChanged();
        }
        private void ExecuteDeleteSelectedQuery(object? parameter)
        {
            if (SelectedQueryInList != null && !_isNewQueryMode)
            {
                var result = MessageBox.Show($"Are you sure you want to delete query '{SelectedQueryInList.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    string deletedName = SelectedQueryInList.Name;
                    Queries.Remove(SelectedQueryInList);
                    SelectedQueryInList = null; // Deselect
                    EditingQueryCopy = null; // Clear form
                    StatusMessage = $"Query '{deletedName}' deleted.";
                }
            }
        }

        private void ExecuteSaveQuery(object? parameter)
        {
            if (EditingQueryCopy == null) return;

            // Validate EditingQueryCopy.Name (must not be empty)
            if (string.IsNullOrWhiteSpace(EditingQueryCopy.Name))
            {
                MessageBox.Show("Query Name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Update TargetConnectionIds from the UI state of AvailableConnectionsForTargeting
            if (EditingQueryCopy != null) // Ensure EditingQueryCopy is not null
            {
                EditingQueryCopy.TargetConnectionIds.Clear();
                foreach (var selectableConn in AvailableConnectionsForTargeting.Where(sc => sc.IsSelected))
                {
                    EditingQueryCopy.TargetConnectionIds.Add(selectableConn.ConnectionId);
                }
            }


            if (_isNewQueryMode) // Saving a brand new query
            {
                Queries.Add(EditingQueryCopy); // Add the copy to the main list
                SelectedQueryInList = EditingQueryCopy; // Select it in the main list
                _isNewQueryMode = false;
                StatusMessage = $"Query '{EditingQueryCopy.Name}' added and saved.";
            }
            else if (SelectedQueryInList != null) // Saving changes to an existing query
            {
                // Apply changes from EditingQueryCopy to SelectedQueryInList
                SelectedQueryInList.Name = EditingQueryCopy.Name;
                SelectedQueryInList.Description = EditingQueryCopy.Description;
                SelectedQueryInList.FolderPath = EditingQueryCopy.FolderPath;
                SelectedQueryInList.SqlTemplates = new ObservableCollection<SqlTemplateEditable>(EditingQueryCopy.SqlTemplates.Select(st => new SqlTemplateEditable(st.SqlText)));
                SelectedQueryInList.Parameters = new ObservableCollection<ParameterDefinition>(EditingQueryCopy.Parameters.Select(p => new ParameterDefinition
                { // Again, simple copy. Consider a true Clone method on ParameterDefinition if it gets complex.
                    PlaceholderName = p.PlaceholderName,
                    DisplayName = p.DisplayName,
                    DataType = p.DataType,
                    IsRequired = p.IsRequired,
                    DefaultValue = p.DefaultValue,
                    Tooltip = p.Tooltip,
                    ValueListOptions = p.ValueListOptions != null ? new List<string>(p.ValueListOptions) : new List<string>()
                }));
                SelectedQueryInList.TargetConnectionIds = new List<Guid>(EditingQueryCopy.TargetConnectionIds);
                StatusMessage = $"Query '{SelectedQueryInList.Name}' saved.";
                // Force refresh of the ListBox item if DisplayMemberPath doesn't pick up Name change on its own
                // This can be done by temporarily removing and re-adding, or using a more complex refresh mechanism
                // For ObservableCollection, changing a property of an item should reflect if the item implements INPC.
                // If QueryDefinition implemented INPC, this would be automatic.
                // For now, let's hope direct property change is enough for DisplayMemberPath="Name".
            }
            ((RelayCommand)DeleteSelectedQueryCommand).RaiseCanExecuteChanged();
        }
        private void ExecuteRevertQuery(object? parameter)
        {
            if (_isNewQueryMode) // Reverting a new, unsaved query
            {
                EditingQueryCopy = null; // Just clear the form
                _isNewQueryMode = false;
                StatusMessage = "New query cancelled.";
            }
            else if (SelectedQueryInList != null) // Reverting an existing query
            {
                LoadQueryForEditing(SelectedQueryInList); // Reload original from list
                StatusMessage = $"Changes to '{SelectedQueryInList.Name}' reverted.";
            }
        }

        private void ExecuteAddSqlTemplate(object? parameter)
        {
            if (EditingQueryCopy != null)
            {
                EditingQueryCopy.SqlTemplates.Add(new SqlTemplateEditable("-- New SQL Statement --"));
                StatusMessage = "Added SQL template.";
            }
        }
        private void ExecuteRemoveLastSqlTemplate(object? parameter)
        {
            if (EditingQueryCopy != null && EditingQueryCopy.SqlTemplates.Any())
            {
                EditingQueryCopy.SqlTemplates.RemoveAt(EditingQueryCopy.SqlTemplates.Count - 1);
                StatusMessage = "Removed last SQL template.";
            }
        }

        private void ExecuteAddParameter(object? parameter)
        {
            if (EditingQueryCopy != null)
            {
                var newParam = new ParameterDefinition { PlaceholderName = "@NewParam", DisplayName = "New Parameter" };
                EditingQueryCopy.Parameters.Add(newParam);
                SelectedParameterForEditing = newParam;
                StatusMessage = "Added parameter.";
            }
        }
        private void ExecuteRemoveSelectedParameter(object? parameter)
        {
            if (EditingQueryCopy != null && SelectedParameterForEditing != null)
            {
                EditingQueryCopy.Parameters.Remove(SelectedParameterForEditing);
                SelectedParameterForEditing = null;
                StatusMessage = "Removed parameter.";
            }
        }

        private async Task ExecuteCheckSyntaxAsync(SqlTemplateEditable? templateToTest)
        {
            if (templateToTest == null || EditingQueryCopy == null || ValidationConnection == null)
            {
                StatusMessage = "Please select a SQL template and a validation connection.";
                return;
            }

            try
            {
                // Decrypt the password for the validation connection if needed
                if (!ValidationConnection.UseWindowsAuth && ValidationConnection.EncryptedPassword != null)
                {
                    ValidationConnection.DecryptedPasswordForCurrentOperation =
                        _encryptionService.DecryptToString(ValidationConnection.EncryptedPassword);

                    if (ValidationConnection.DecryptedPasswordForCurrentOperation == null)
                    {
                        MessageBox.Show("Failed to decrypt password for the selected validation connection.", "Decryption Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // --- Perform Validation ---
                StatusMessage = $"Validating syntax against '{ValidationConnection.ConnectionName}'...";
                var result = await _sqlValidationService.ValidateAsync(templateToTest.SqlText, EditingQueryCopy, ValidationConnection);

                var sb = new StringBuilder();
                sb.AppendLine(result.IsValid ? "Validation Succeeded:" : "Validation Failed:");
                foreach (var err in result.Errors)
                {
                    sb.AppendLine($"- {err}");
                }
                MessageBox.Show(sb.ToString(), "Syntax Check Result", MessageBoxButton.OK, result.IsValid ? MessageBoxImage.Information : MessageBoxImage.Error);
                StatusMessage = "Syntax check complete.";
            }
            catch (Exception ex)
            {
                // Catch any other unexpected errors
                MessageBox.Show($"An unexpected error occurred during syntax validation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Syntax check failed with an unexpected error.";
            }
            finally
            {
                // --- IMPORTANT: Clear the decrypted password from memory ---
                if (ValidationConnection != null)
                {
                    ValidationConnection.DecryptedPasswordForCurrentOperation = null;
                }
            }
        }
        
        private void ExecuteCloseWindow(Window? window)
        {
            // Prompt to save if dirty (future enhancement)
            window?.Close();
        }

    }
}