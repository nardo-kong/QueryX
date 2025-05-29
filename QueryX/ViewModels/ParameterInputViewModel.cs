using QueryX.Models; // Reference Models

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
                if (SetProperty(ref _value, value))
                {
                    // Re-validate whenever the value changes
                    IsValid(out _);
                }
            }
        }

        public IEnumerable<string> OptionsForList => Definition.ValueListOptions ?? Enumerable.Empty<string>();

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
            if (definition.DataType == ParameterDataType.List && definition.ValueListOptions?.Any() == true)
            {
                // For Lists, check if default value is in the list, otherwise use the first item or null
                string? defaultStr = definition.DefaultValue?.ToString();
                _value = definition.ValueListOptions.Contains(defaultStr) ? defaultStr : definition.ValueListOptions.FirstOrDefault();
            }
            else
            {
                _value = definition.DefaultValue;
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

    }
}