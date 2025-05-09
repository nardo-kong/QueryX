using QueryX.Models; // Reference Models

namespace QueryX.ViewModels // Ensure namespace matches
{
    public class ParameterInputViewModel : ViewModelBase // Inherit from ViewModelBase
    {
        private object? _value; // Field to hold the user-entered value

        // The definition containing metadata (Name, Type, IsRequired etc.)
        public ParameterDefinition Definition { get; }

        // The value entered by the user, bound to the UI control
        public object? Value
        {
            get => _value;
            set => SetProperty(ref _value, value); // Use SetProperty for notification
        }

        // Constructor
        public ParameterInputViewModel(ParameterDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            // Set initial value from default defined in the parameter definition
            _value = Definition.DefaultValue;
        }

        // Basic validation example (can be expanded)
        public bool IsValid(out string? errorMessage)
        {
            errorMessage = null;
            if (Definition.IsRequired && (_value == null || string.IsNullOrWhiteSpace(_value.ToString())))
            {
                errorMessage = $"{Definition.DisplayName} is required.";
                return false;
            }

            // TODO: Add type-specific validation based on Definition.DataType (e.g., check if Int is a valid integer)

            return true;
        }
    }
}