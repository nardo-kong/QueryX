using System;
using System.Text.Json.Serialization;

namespace QueryX.Models // 确保命名空间正确
{
    // 定义支持的数据库类型枚举
    public enum DatabaseType
    {
        SQLServer,
        MySQL,
        PostgreSQL,
        SQLite,
        Oracle // 可以根据需要增删
    }

    public class DatabaseConnectionInfo
    {
        // 唯一标识符，方便管理
        public Guid Id { get; set; } = Guid.NewGuid();

        // 用户给连接起的名字，方便识别
        public string ConnectionName { get; set; } = string.Empty;

        // 数据库类型
        public DatabaseType DbType { get; set; } = DatabaseType.SQLServer;

        // Server/Host: For Oracle, this is the hostname or IP address. For SQLite, the file path.
        public string Server { get; set; } = string.Empty;

        // 数据库名称
        public string DatabaseName { get; set; } = string.Empty;

        // 是否使用 Windows 集成认证
        public bool UseWindowsAuth { get; set; } = true;

        // 用户名（如果 UseWindowsAuth 为 false）
        public string? UserName { get; set; } // 可空，如果使用 Windows 认证则不需要

        // 密码（如果 UseWindowsAuth 为 false）
        // !! 安全警告: 直接存储明文密码非常不安全 !! 后续步骤需要实现加密存储，这里暂时用 string 占位
        //public string? Password { get; set; } // 可空，且需要安全处理

        // --- NEW PROPERTIES for Encrypted Password ---
        /// <summary>
        /// Stores the password encrypted using DPAPI. This is what gets serialized.
        /// </summary>
        public byte[]? EncryptedPassword { get; set; }

        /// <summary>
        /// Non-serialized property to hold the plain text password temporarily for the current operation.
        /// This should be cleared immediately after use.
        /// </summary>
        [JsonIgnore] // Ensures this is NOT serialized to config.json
        public string? DecryptedPasswordForCurrentOperation { get; set; }

        // 可以添加其他连接参数，如端口、连接超时等
        // public int? Port { get; set; }
        // public int ConnectionTimeout { get; set; } = 30; // 默认30秒

        // 重写 ToString() 方法方便在下拉列表等地方显示
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(ConnectionName) ? $"New Connection ({DbType})" : $"{ConnectionName} ({DbType})";
        }
    }
}