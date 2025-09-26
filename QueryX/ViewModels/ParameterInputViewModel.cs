using QueryX.Models; // Reference Models
using QueryX.Services; // Reference Services
using System.Text.Json.Serialization;
using System.Linq;

namespace QueryX.ViewModels // Ensure namespace matches
{
    public class ParameterInputViewModel : ViewModelBase // Inherit from ViewModelBase
    {
        private object? _value; // Field to hold the user-entered value
        private string? _errorMessage;

        // The definition containing metadata (Name, Type, IsRequired etc.)
        public ParameterDefinition Definition { get; }

        // The value entered by the user, bound to the UI control
        public object? Value
        {
            get => _value;
            set
            {
                // Handle ListOption objects - extract the Value property
                if (value is ListOption listOption)
                {
                    value = listOption.Value;
                }
                
                if (SetProperty(ref _value, value))
                {
                    // Re-validate whenever the value changes
                    IsValid(out _);
                    
                    // Notify display text change for List parameters
                    if (Definition.DataType == ParameterDataType.List)
                    {
                        OnPropertyChanged(nameof(CurrentValueDisplayText));
                    }
                }
            }
        }

        public IEnumerable<string> OptionsForList => Definition.ValueListOptions ?? Enumerable.Empty<string>();

        // Options for dynamic lists loaded from SQL queries
        public IEnumerable<ListOption> DynamicOptionsForList => Definition.LoadedListOptions ?? Enumerable.Empty<ListOption>();

        // Combined property that returns appropriate options based on parameter configuration
        [JsonIgnore]
        public IEnumerable<object> AllOptionsForList
        {
            get
            {
                if (Definition.UsesSqlForOptions && Definition.LoadedListOptions?.Any() == true)
                {
                    return Definition.LoadedListOptions;
                }
                else if (Definition.ValueListOptions?.Any() == true)
                {
                    return Definition.ValueListOptions.Select(opt => new ListOption(opt));
                }
                return Enumerable.Empty<object>();
            }
        }

        // Indicates if options are currently being loaded
        private bool _isLoadingOptions;
        [JsonIgnore]
        public bool IsLoadingOptions
        {
            get => _isLoadingOptions;
            set => SetProperty(ref _isLoadingOptions, value);
        }

        // Error message specific to option loading
        private string? _optionLoadingError;
        [JsonIgnore]
        public string? OptionLoadingError
        {
            get => _optionLoadingError;
            set => SetProperty(ref _optionLoadingError, value);
        }

        // Gets the display text for the current value (useful for List parameters)
        [JsonIgnore]
        public string CurrentValueDisplayText
        {
            get
            {
                if (Definition.DataType == ParameterDataType.List && _value != null)
                {
                    string valueStr = _value.ToString() ?? "";
                    
                    // Try to find matching option to get display text
                    if (Definition.UsesSqlForOptions && Definition.LoadedListOptions != null)
                    {
                        var matchingOption = Definition.LoadedListOptions
                            .FirstOrDefault(opt => string.Equals(opt.Value, valueStr, StringComparison.OrdinalIgnoreCase));
                        return matchingOption?.DisplayText ?? valueStr;
                    }
                    else if (Definition.ValueListOptions != null)
                    {
                        // For static options, value and display are the same
                        return Definition.ValueListOptions.Contains(valueStr) ? valueStr : valueStr;
                    }
                }
                
                return _value?.ToString() ?? "";
            }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        // Constructor
        public ParameterInputViewModel(ParameterDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            // Set default value
            if (definition.DataType == ParameterDataType.List)
            {
                SetDefaultValueForList();
            }
            else
            {
                _value = definition.DefaultValue;
            }
        }

        private void SetDefaultValueForList()
        {
            string? defaultStr = Definition.DefaultValue?.ToString();

            if (Definition.UsesSqlForOptions && Definition.LoadedListOptions?.Any() == true)
            {
                // Check if default value matches any loaded option
                var matchingOption = Definition.LoadedListOptions.FirstOrDefault(opt => 
                    string.Equals(opt.Value, defaultStr, StringComparison.OrdinalIgnoreCase));
                
                _value = matchingOption?.Value ?? Definition.LoadedListOptions.FirstOrDefault()?.Value;
            }
            else if (Definition.ValueListOptions?.Any() == true)
            {
                // For static lists, check if default value is in the list
                _value = Definition.ValueListOptions.Contains(defaultStr) ? defaultStr : Definition.ValueListOptions.FirstOrDefault();
            }
            else
            {
                _value = null;
            }
        }

        public bool IsValid(out string? validationMessage)
        {
            // First, check for required
            if (Definition.IsRequired && (_value == null || string.IsNullOrWhiteSpace(_value.ToString())))
            {
                validationMessage = $"{Definition.DisplayName} is required.";
                ErrorMessage = validationMessage;
                return false;
            }

            // Then, perform type-specific validation
            if (_value != null && !string.IsNullOrWhiteSpace(_value.ToString()))
            {
                string valStr = _value.ToString()!;
                switch (Definition.DataType)
                {
                    case ParameterDataType.Int:
                        if (!int.TryParse(valStr, out _))
                        {
                            validationMessage = "Value must be a valid integer (e.g., 123).";
                            ErrorMessage = validationMessage;
                            return false;
                        }
                        break;
                    case ParameterDataType.Decimal:
                        if (!decimal.TryParse(valStr, out _))
                        {
                            validationMessage = "Value must be a valid decimal number (e.g., 123.45).";
                            ErrorMessage = validationMessage;
                            return false;
                        }
                        break;
                        // DateTime is handled by DatePicker, Boolean by CheckBox, List by ComboBox.
                        // String type has no specific validation here but could (e.g., regex).
                }
            }

            // If all checks pass
            validationMessage = null;
            ErrorMessage = null; // Clear any previous error
            return true;
        }

        /// <summary>
        /// Loads dynamic options for list parameters that use SQL queries
        /// </summary>
        public async Task LoadDynamicOptionsAsync(ParameterOptionsService optionsService, DatabaseConnectionInfo connectionInfo)
        {
            if (Definition.DataType != ParameterDataType.List || !Definition.UsesSqlForOptions)
                return;

            IsLoadingOptions = true;
            OptionLoadingError = null;

            try
            {
                var options = await optionsService.LoadParameterOptionsAsync(Definition, connectionInfo);
                Definition.LoadedListOptions = options;
                
                // Update the current value if needed
                SetDefaultValueForList();
                
                // Notify that options have changed
                OnPropertyChanged(nameof(AllOptionsForList));
                OnPropertyChanged(nameof(DynamicOptionsForList));
                OnPropertyChanged(nameof(CurrentValueDisplayText));
            }
            catch (Exception ex)
            {
                OptionLoadingError = $"Failed to load options: {ex.Message}";
                Definition.LoadedListOptions = new List<ListOption>();
                
                // Still notify that options changed (now empty)
                OnPropertyChanged(nameof(AllOptionsForList));
                OnPropertyChanged(nameof(DynamicOptionsForList));
            }
            finally
            {
                IsLoadingOptions = false;
            }
        }

    }
}