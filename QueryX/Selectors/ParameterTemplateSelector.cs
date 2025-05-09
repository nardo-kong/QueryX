using QueryX.Models; // Reference Models namespace
using QueryX.ViewModels; // Reference ViewModels namespace
using System.Windows;
using System.Windows.Controls;

namespace QueryX.Selectors
{
    public class ParameterTemplateSelector : DataTemplateSelector
    {
        // Define properties to hold the templates defined in XAML Resources
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? IntegerTemplate { get; set; }
        public DataTemplate? DecimalTemplate { get; set; }
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? DateTimeTemplate { get; set; }
        // Add more templates as needed (e.g., for Lists)

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            // 'item' is the object being bound, which should be a ParameterInputViewModel
            if (item is ParameterInputViewModel parameterVM && container is FrameworkElement element)
            {
                // Check the DataType defined in the ParameterDefinition
                switch (parameterVM.Definition.DataType)
                {
                    case ParameterDataType.String:
                        // Find the template with x:Key="StringParameterTemplate" in resources
                        // return element.FindResource("StringParameterTemplate") as DataTemplate; // Alternative way
                        return StringTemplate; // Use property injection

                    case ParameterDataType.Int:
                        return IntegerTemplate;

                    case ParameterDataType.Decimal:
                        return DecimalTemplate;

                    case ParameterDataType.Boolean:
                        return BooleanTemplate;

                    case ParameterDataType.DateTime:
                        return DateTimeTemplate;

                    // Handle other types or return default/string template
                    default:
                        return StringTemplate; // Fallback to string template
                }
            }

            // Fallback if item is not the expected type
            return base.SelectTemplate(item, container);
        }
    }
}