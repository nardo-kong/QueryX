using System;
using System.Collections.Generic;
using System.Linq; // 需要引入 Linq
using System.Collections.ObjectModel;

namespace QueryX.Models // 确保命名空间正确
{
    public class QueryDefinition
    {
        // 唯一标识符
        public Guid Id { get; set; } = Guid.NewGuid();

        // 用户定义的查询名称 (例如: "查询活跃用户", "按订单号查找商品")
        public string Name { get; set; } = string.Empty;

        // 对查询功能的描述
        public string? Description { get; set; }

        // SQL 查询语句模板列表。可以包含一个或多个SQL语句。
        // 使用占位符表示参数 (例如: "SELECT * FROM Users WHERE UserID = @UserID AND IsActive = @IsActive")
        //public List<string> SqlTemplates { get; set; } = new List<string>();
        public ObservableCollection<SqlTemplateEditable> SqlTemplates { get; set; } = new ObservableCollection<SqlTemplateEditable>();

        /// <summary>
        /// List of DatabaseConnectionInfo Ids that this query is intended for or compatible with.
        /// If empty, it might imply compatibility with any/all or a default connection.
        /// </summary>
        public List<Guid> TargetConnectionIds { get; set; } = new List<Guid>();

        // 此查询所需参数的定义列表
        public ObservableCollection<ParameterDefinition> Parameters { get; set; } = new ObservableCollection<ParameterDefinition>();

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? $"New Query ({Id})" : Name;
        }

        // （可选）辅助方法：根据占位符名称查找参数定义
        public ParameterDefinition? GetParameterByName(string placeholderName)
        {
            // 忽略大小写和可能的前缀 (@, :) 进行比较
            return Parameters.FirstOrDefault(p =>
                string.Equals(p.PlaceholderName.TrimStart('@', ':'), placeholderName.TrimStart('@', ':'), StringComparison.OrdinalIgnoreCase));
        }
    }
}