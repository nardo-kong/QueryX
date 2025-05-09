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
        // List // 可以后续添加对下拉列表的支持
    }

    public class ParameterDefinition
    {
        // SQL 模板中使用的占位符名称 (例如: @UserID, :startDate)
        public string PlaceholderName { get; set; } = string.Empty;

        // 在 UI 中显示的友好名称 (例如: "用户ID", "开始日期")
        public string DisplayName { get; set; } = string.Empty;

        // 参数的数据类型，用于 UI 生成和验证
        public ParameterDataType DataType { get; set; } = ParameterDataType.String;

        // 该参数是否为必填项
        public bool IsRequired { get; set; } = true;

        // 参数的默认值 (可以是 null)
        public object? DefaultValue { get; set; }

        // 在 UI 中显示的提示信息 (Tooltip)
        public string? Tooltip { get; set; }

        // （未来扩展）用于下拉列表类型参数的选项来源
        // 可以是固定的值列表字符串，或是一个用于获取选项的SQL查询
        // public string? ListOptionsSource { get; set; }

        public override string ToString()
        {
            return $"{DisplayName} ({PlaceholderName}) - {DataType}{(IsRequired ? ", Required" : "")}";
        }
    }
}