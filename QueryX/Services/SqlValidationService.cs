using QueryX.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QueryX.Services
{
    public class SqlValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class SqlValidationService
    {
        private readonly DatabaseService _databaseService;

        public SqlValidationService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<SqlValidationResult> ValidateAsync(string sql, QueryDefinition queryDef, DatabaseConnectionInfo connectionInfo)
        {
            var result = new SqlValidationResult();

            // 1. Simple Linter Checks
            if (string.IsNullOrWhiteSpace(sql))
            {
                result.Errors.Add("SQL statement cannot be empty.");
            }
            if (sql.Trim().EndsWith(";"))
            {
                result.Errors.Add("SQL statement should not end with a semicolon (;).");
            }
            // Add more simple checks here...

            // 2. Placeholder Style Check
            bool hasAt = sql.Contains("@");
            bool hasColon = sql.Contains(":");
            if (connectionInfo.DbType == DatabaseType.Oracle && hasAt)
            {
                result.Errors.Add("Oracle uses ':' for parameters, but '@' was found.");
            }
            else if (connectionInfo.DbType != DatabaseType.Oracle && hasColon)
            {
                result.Errors.Add("Non-Oracle databases use '@' for parameters, but ':' was found.");
            }

            if (result.Errors.Any())
            {
                result.IsValid = false;
                return result;
            }

            // 3. Database "Dry Run" Syntax Check
            DbConnection? connection = null;
            try
            {
                string sanitizedSql = SanitizeSqlForValidation(sql, queryDef.Parameters);
                string validationSql = WrapForValidation(sanitizedSql, connectionInfo.DbType);

                string connectionString = _databaseService.BuildConnectionString(connectionInfo);
                connection = _databaseService.CreateDbConnection(connectionInfo.DbType, connectionString); // Assuming CreateDbConnection is now public
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = validationSql;
                    // For EXPLAIN, ExecuteReader is appropriate. For SET NOEXEC, ExecuteNonQuery.
                    if (connectionInfo.DbType == DatabaseType.SQLServer)
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // We don't need to read the results, just ensure no exception was thrown.
                        }
                    }
                }
                result.Errors.Add("Syntax appears to be valid.");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Database Error: {ex.Message}");
            }
            finally
            {
                if (connection != null) await connection.CloseAsync();
            }

            return result;
        }

        private string SanitizeSqlForValidation(string sql, IEnumerable<ParameterDefinition> parameters)
        {
            // Replace all known parameters with NULL for validation
            foreach (var param in parameters)
            {
                sql = Regex.Replace(sql, param.PlaceholderName, "NULL", RegexOptions.IgnoreCase);
            }
            return sql;
        }

        private string WrapForValidation(string sql, DatabaseType dbType)
        {
            switch (dbType)
            {
                case DatabaseType.SQLServer:
                    return $"SET NOEXEC ON; {sql}; SET NOEXEC OFF;";
                case DatabaseType.PostgreSQL:
                case DatabaseType.MySQL:
                case DatabaseType.SQLite:
                    return $"EXPLAIN {sql}";
                case DatabaseType.Oracle:
                    return $"EXPLAIN PLAN FOR {sql}";
                default:
                    return sql; // Cannot validate
            }
        }
    }
}