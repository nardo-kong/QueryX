using System.Collections.Generic; // 需要引入

namespace QueryX.Models // 确保命名空间正确
{
    public class AppConfiguration
    {
        // 存储所有数据库连接信息
        public List<DatabaseConnectionInfo> Connections { get; set; } = new List<DatabaseConnectionInfo>();

        // 存储所有查询定义
        public List<QueryDefinition> Queries { get; set; } = new List<QueryDefinition>();

        // 可以在这里添加其他全局应用程序设置，例如：
        // public string LastUsedConnectionId { get; set; }
        // public int MaxResultsToDisplay { get; set; } = 1000;
    }
}