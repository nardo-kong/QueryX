using QueryX.Models; // Reference Models
using System;
using System.Data.Common; // For DbConnection, DbProviderFactories (might need manual registration for some providers)
using System.Text; // For StringBuilder
using System.Threading.Tasks; // For Task/async
// Add using statements for each provider you installed:
using System.Data.SqlClient;
using Npgsql;
using MySql.Data.MySqlClient;
using Microsoft.Data.Sqlite;
using Oracle.ManagedDataAccess.Client;
using System.Diagnostics;

namespace QueryX.Services // Ensure namespace matches your project
{
    public class DatabaseService
    {
        // Builds the connection string based on the DatabaseConnectionInfo
        public string BuildConnectionString(DatabaseConnectionInfo connectionInfo)
        {
            var builder = new StringBuilder();
            try
            {
                switch (connectionInfo.DbType)
                {
                    case DatabaseType.SQLServer:
                        var sqlBuilder = new SqlConnectionStringBuilder
                        {
                            DataSource = connectionInfo.Server,
                            InitialCatalog = connectionInfo.DatabaseName,
                            IntegratedSecurity = connectionInfo.UseWindowsAuth,
                            UserID = connectionInfo.UseWindowsAuth ? "" : connectionInfo.UserName,
                            Password = connectionInfo.UseWindowsAuth ? "" : connectionInfo.Password // TODO: Decrypt password if stored encrypted
                        };
                        // sqlBuilder.Encrypt = true; // Consider adding security options
                        // sqlBuilder.TrustServerCertificate = true; // Use appropriately
                        return sqlBuilder.ConnectionString;

                    case DatabaseType.PostgreSQL:
                        var npgsqlBuilder = new NpgsqlConnectionStringBuilder
                        {
                            Host = connectionInfo.Server,
                            Database = connectionInfo.DatabaseName,
                            // Pooling = true, // Configure other options as needed
                        };
                        if (connectionInfo.UseWindowsAuth)
                        {
                            // Npgsql does not support 'IntegratedSecurity' directly.  
                            // Instead, use 'Username' as 'IntegratedSecurity' and omit the password.  
                            npgsqlBuilder.Username = "IntegratedSecurity";
                            npgsqlBuilder.Password = null;
                        }
                        else
                        {
                            npgsqlBuilder.Username = connectionInfo.UserName;
                            npgsqlBuilder.Password = connectionInfo.Password; // TODO: Decrypt  
                        }
                        return npgsqlBuilder.ConnectionString;

                    case DatabaseType.MySQL:
                        var mysqlBuilder = new MySqlConnectionStringBuilder
                        {
                            Server = connectionInfo.Server,
                            Database = connectionInfo.DatabaseName,
                            UserID = connectionInfo.UserName,
                            Password = connectionInfo.Password, // TODO: Decrypt
                            IntegratedSecurity = connectionInfo.UseWindowsAuth, // May require specific connector/server config
                                                                                // PersistSecurityInfo = false, // Good practice
                                                                                // SslMode = MySqlSslMode.Preferred // Consider SSL
                        };
                        return mysqlBuilder.ConnectionString;

                    case DatabaseType.SQLite:
                        var sqliteBuilder = new SqliteConnectionStringBuilder
                        {
                            DataSource = connectionInfo.Server, // Server property holds the file path for SQLite
                            Mode = SqliteOpenMode.ReadWriteCreate // Default mode, adjust if needed
                            // Password = connectionInfo.Password // If using encrypted SQLite
                        };
                        return sqliteBuilder.ConnectionString;

                    case DatabaseType.Oracle:
                        // Example - Adjust based on Oracle connection string format
                        // Needs Oracle.ManagedDataAccess.Core package
                        var oraBuilder = new OracleConnectionStringBuilder
                        {
                            DataSource = connectionInfo.Server, // Often in format "host:port/service_name" or using TNSNames
                            UserID = connectionInfo.UserName,
                            Password = connectionInfo.Password // TODO: Decrypt
                            // IntegratedSecurity = connectionInfo.UseWindowsAuth // Check Oracle syntax for this
                        };
                        return oraBuilder.ConnectionString;
                        
                        throw new NotSupportedException("Oracle connection string building not fully implemented.");


                    default:
                        throw new NotSupportedException($"Database type '{connectionInfo.DbType}' is not supported for connection string building.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building connection string for {connectionInfo.ConnectionName}: {ex.Message}");
                return string.Empty; // Return empty on error
            }
        }

        // Tests a database connection asynchronously
        // Returns a tuple: (bool IsSuccess, string Message)
        public async Task<(bool IsSuccess, string Message)> TestConnectionAsync(DatabaseConnectionInfo connectionInfo)
        {
            string connectionString = BuildConnectionString(connectionInfo);
            if (string.IsNullOrEmpty(connectionString))
            {
                return (false, "Failed to build connection string.");
            }

            DbConnection? connection = null; // Use base class DbConnection
            try
            {
                // Create the appropriate connection object based on DbType
                switch (connectionInfo.DbType)
                {
                    case DatabaseType.SQLServer:
                        connection = new SqlConnection(connectionString);
                        break;
                    case DatabaseType.PostgreSQL:
                        connection = new NpgsqlConnection(connectionString);
                        break;
                    case DatabaseType.MySQL:
                        connection = new MySqlConnection(connectionString);
                        break;
                    case DatabaseType.SQLite:
                        connection = new SqliteConnection(connectionString);
                        break;
                    case DatabaseType.Oracle:
                        connection = new OracleConnection(connectionString);
                        break;
                        //throw new NotSupportedException("Oracle connection testing not fully implemented.");
                    default:
                        throw new NotSupportedException($"Database type '{connectionInfo.DbType}' is not supported for testing.");
                }

                // Asynchronously open the connection
                await connection.OpenAsync();

                // If OpenAsync() completes without exception, connection is successful
                return (true, "Connection successful!");
            }
            catch (Exception ex)
            {
                // Log the full exception for debugging
                System.Diagnostics.Debug.WriteLine($"Connection test failed for '{connectionInfo.ConnectionName}': {ex}");
                // Return a user-friendly message
                return (false, $"Connection failed: {ex.Message}");
            }
            finally
            {
                // Ensure the connection is closed and disposed even if errors occur
                if (connection != null)
                {
                    await connection.CloseAsync(); // Close async
                    await connection.DisposeAsync(); // Dispose async
                }
            }
        }
    }
}