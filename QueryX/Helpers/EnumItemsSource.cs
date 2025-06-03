using QueryX.Models; // For ParameterDataType
using System;
using System.Linq;

namespace QueryX.Helpers
{
    public static class EnumItemsSource
    {
        public static ParameterDataType[] ParameterDataTypes { get; } =
            Enum.GetValues(typeof(ParameterDataType)).Cast<ParameterDataType>().ToArray();
    }
}