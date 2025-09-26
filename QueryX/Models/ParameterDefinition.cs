using System.Text.Json.Serialization;
using System.Linq; // For LINQ operations
using QueryX.ViewModels; // For ViewModelBase

namespace QueryX.Models // 确保命名空间正确
{
    // 定义参数的数据类型枚举
    public enum ParameterDataType
    {
        String,
        Int,
        Decimal,
        DateTime,
        Boolean,
        List // 可以后续添加对下拉列表的支持
    }

    // Represents an option for a List parameter with separate value and display text
    public class ListOption
    {
        public string Value { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;

        public ListOption() { }

        public ListOption(string value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }

        // Constructor for when value and display are the same
        public ListOption(string valueAndDisplay) : this(valueAndDisplay, valueAndDisplay) { }

        public override string ToString() => DisplayText;
    }

    public class ParameterDefinition: ViewModelBase // 继承自 ViewModelBase 以支持 INotifyPropertyChanged
    {
        // SQL 模板中使用的占位符名称 (例如: @UserID, :startDate)
        private string _placeholderName = string.Empty;
        public string PlaceholderName
        {
            get => _placeholderName;
            set => SetProperty(ref _placeholderName, value);
        }

        // 在 UI 中显示的友好名称 (例如: "用户ID", "开始日期")
        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        // 参数的数据类型，用于 UI 生成和验证
        private ParameterDataType _dataType = ParameterDataType.String;
        public ParameterDataType DataType
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }

        // 该参数是否为必填项
        private bool _isRequired = true;
        public bool IsRequired
        {
            get => _isRequired;
            set => SetProperty(ref _isRequired, value);
        }

        // 参数的默认值 (可以是 null)
        private object? _defaultValue;
        public object? DefaultValue
        {
            get => _defaultValue;
            set => SetProperty(ref _defaultValue, value);
        }

        // 在 UI 中显示的提示信息 (Tooltip)
        private string? _tooltip;
        public string? Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        // Advanced parameter sourcing for lists
        // When set, this SQL query will be executed to populate the list options
        private string? _listOptionsSourceQuery;
        public string? ListOptionsSourceQuery
        {
            get => _listOptionsSourceQuery;
            set => SetProperty(ref _listOptionsSourceQuery, value);
        }

        // Connection ID to use for executing the ListOptionsSourceQuery
        private Guid? _listOptionsConnectionId;
        public Guid? ListOptionsConnectionId
        {
            get => _listOptionsConnectionId;
            set => SetProperty(ref _listOptionsConnectionId, value);
        }

        // Column names for value and display text (e.g., "CustomerID", "CustomerName")
        // If only ValueColumn is specified, it will be used for both value and display
        private string? _listOptionsValueColumn;
        public string? ListOptionsValueColumn
        {
            get => _listOptionsValueColumn;
            set => SetProperty(ref _listOptionsValueColumn, value);
        }

        private string? _listOptionsDisplayColumn;
        public string? ListOptionsDisplayColumn
        {
            get => _listOptionsDisplayColumn;
            set => SetProperty(ref _listOptionsDisplayColumn, value);
        }

        // Indicates whether this parameter uses SQL query for options (true) or static options (false)
        [JsonIgnore]
        public bool UsesSqlForOptions => !string.IsNullOrWhiteSpace(ListOptionsSourceQuery);


        // For DataType = List, this holds the predefined string options.
        private List<string>? _valueListOptions = new List<string>();
        public List<string>? ValueListOptions 
        { 
            get => _valueListOptions;
            set => SetProperty(ref _valueListOptions, value);
        }

        // Dynamically loaded list options from SQL query
        private List<ListOption>? _loadedListOptions;
        [JsonIgnore]
        public List<ListOption>? LoadedListOptions
        {
            get => _loadedListOptions;
            set => SetProperty(ref _loadedListOptions, value);
        }

        // Helper property for easy binding in the Query Manager's DataGrid.
        [JsonIgnore]
        public string ValueListOptionsString
        {
            get => ValueListOptions != null ? string.Join(",", ValueListOptions) : string.Empty;
            set
            {
                var newValue = value?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim()).ToList() ?? new List<string>();
                SetProperty(ref _valueListOptions, newValue, nameof(ValueListOptions));
                OnPropertyChanged(); // Notify that ValueListOptionsString itself changed
            }
        }

        public override string ToString()
        {
            return $"{DisplayName} ({PlaceholderName}) - {DataType}{(IsRequired ? ", Required" : "")}";
        }
    }
}