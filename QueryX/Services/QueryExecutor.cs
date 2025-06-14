﻿using QueryX.Models;
using QueryX.Logging; // For logging
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common; // Base classes for ADO.NET
using System.Diagnostics; // For Stopwatch
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
// Add specific provider using statements
using System.Data.SqlClient;
using Npgsql;
using MySql.Data.MySqlClient;
using Microsoft.Data.Sqlite;
using Oracle.ManagedDataAccess.Client;

namespace QueryX.Services // Ensure namespace matches
{
    public class QueryExecutor
    {
        private readonly DatabaseService _databaseService;
        private readonly SqlParser _sqlParser; // Optional, if parameter validation is desired here
        private readonly EncryptionService _encryptionService;

        public QueryExecutor(DatabaseService databaseService, SqlParser sqlParser, EncryptionService encryptionService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _sqlParser = sqlParser ?? throw new ArgumentNullException(nameof(sqlParser));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        /// <summary>
        /// Executes one or more SQL statements defined in a QueryDefinition.
        /// </summary>
        /// <param name="queryDefinition">The query definition containing SQL templates and parameter info.</param>
        /// <param name="connectionInfo">Database connection details.</param>
        /// <param name="parameterValues">Dictionary mapping parameter placeholder names (without prefix) to their values.</param>
        /// <param name="cancellationToken">Token to support cancellation.</param>
        /// <returns>A QueryResult object containing results or error information.</returns>
        public async Task<QueryResult> ExecuteAsync(
            QueryDefinition queryDefinition,
            DatabaseConnectionInfo connectionInfo,
            Dictionary<string, object?> parameterValues,
            CancellationToken cancellationToken = default)
        {
            var overallResult = new QueryResult(); // Start with a new, successful result object

            Stopwatch stopwatch = Stopwatch.StartNew();
            DbConnection? connection = null;
            DbTransaction? transaction = null; // Optional: Handle transactions if needed across multiple statements
            DataTable? lastResultTable = null;
            int totalRecordsAffected = 0;
            bool isSelectQuery = false;

            Log.Logger?.Information("Executing query '{QueryName}' on connection '{ConnectionName}'.", queryDefinition.Name, connectionInfo.ConnectionName);

            // --- Decrypt password if needed ---
            if (!connectionInfo.UseWindowsAuth)
            {
                if (connectionInfo.EncryptedPassword != null)
                {
                    try
                    {
                        connectionInfo.DecryptedPasswordForCurrentOperation =
                            _encryptionService.DecryptToString(connectionInfo.EncryptedPassword);
                        if (connectionInfo.DecryptedPasswordForCurrentOperation == null)
                        {
                            return new QueryResult("Error: Failed to decrypt stored password.");
                        }
                    }
                    catch (CryptographicException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Password decryption failed: {ex.Message}");
                        return new QueryResult("Error: Password decryption failed.");
                    }
                }
                else // Not Windows Auth, but no encrypted password stored.
                {
                    return new QueryResult("Error: Password required for this connection but not configured.");
                }
            }

            try
            {
                string connectionString = _databaseService.BuildConnectionString(connectionInfo);
                if (string.IsNullOrEmpty(connectionString))
                {
                    return new QueryResult("Failed to build connection string.");
                }

                // Create Connection based on type
                connection = CreateDbConnection(connectionInfo.DbType, connectionString);
                await connection.OpenAsync(cancellationToken);

                // Optional: Start transaction if multiple SQL statements need atomicity
                // transaction = await connection.BeginTransactionAsync(cancellationToken);

                foreach (SqlTemplateEditable sqlTemplateWrapper in queryDefinition.SqlTemplates)
                {
                    string sqlTemplate = sqlTemplateWrapper.SqlText; 
                    if (string.IsNullOrWhiteSpace(sqlTemplate)) continue;
                    cancellationToken.ThrowIfCancellationRequested(); // Check for cancellation before each statement

                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sqlTemplate;
                        command.Transaction = transaction; // Assign transaction if used

                        // --- Parameter Binding ---
                        // Extract parameters expected by this specific SQL template
                        var expectedParams = _sqlParser.ExtractParameters(sqlTemplate);

                        foreach (string paramName in expectedParams)
                        {
                            ParameterDefinition? paramDef = queryDefinition.GetParameterByName(paramName); // Find definition
                            if (paramDef == null)
                            {
                                // Should not happen if QueryDefinition is well-formed, but handle defensively
                                Debug.WriteLine($"Warning: Parameter '{paramName}' found in SQL but not defined in QueryDefinition.");
                                continue;
                            }

                            // Get value from user input, use default if not provided (or handle optional?)
                            parameterValues.TryGetValue(paramName, out object? paramValue);
                            paramValue ??= paramDef.DefaultValue; // Use default if null

                            // Create DbParameter
                            DbParameter dbParam = command.CreateParameter();
                            // Use placeholder name *from definition* which includes the prefix (@, :)
                            dbParam.ParameterName = paramDef.PlaceholderName;

                            // Set Value (handle DBNull for null values)
                            dbParam.Value = paramValue ?? DBNull.Value;

                            // Set DbType (optional but recommended for type safety)
                            // This mapping needs to be robust
                            dbParam.DbType = MapToDbType(paramDef.DataType);

                            command.Parameters.Add(dbParam);
                        }
                        // --- End Parameter Binding ---


                        // --- Execute Command ---
                        // Determine if it's likely a SELECT statement (basic check)
                        isSelectQuery = IsSelectLikeQuery(sqlTemplate);
                        Debug.WriteLine($"Executing SQL: {sqlTemplate} with parameters: {string.Join(", ", command.Parameters.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value}"))}");

                        if (isSelectQuery)
                        {
                            using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                            {
                                var dataTable = new DataTable();
                                dataTable.Load(reader); // Load reader directly into DataTable
                                dataTable.TableName = $"Result Set {overallResult.ResultTables.Count + 1}";
                                lastResultTable = dataTable; // Store the result table (overwrite previous if multiple SELECTs)
                                overallResult.ResultTables.Add(dataTable);
                            }
                            totalRecordsAffected = 0; // Reset records affected for SELECT
                        }
                        else // Assume INSERT, UPDATE, DELETE, etc.
                        {
                            totalRecordsAffected += await command.ExecuteNonQueryAsync(cancellationToken);
                            lastResultTable = null; // Clear data table for non-SELECT
                        }
                    } // Dispose DbCommand
                } // End foreach sqlTemplate

                // Optional: Commit transaction if used
                // if (transaction != null) await transaction.CommitAsync(cancellationToken);

                overallResult.RecordsAffected = totalRecordsAffected;
                //stopwatch.Stop();

            }
            catch (OperationCanceledException)
            {
                overallResult.IsSuccess = false;
                overallResult.ErrorMessage = "Query execution was cancelled.";
                Log.Logger?.Warning("Query execution was cancelled by the user.");
            }
            catch (Exception ex)
            {
                overallResult.IsSuccess = false;
                overallResult.ErrorMessage = $"Execution failed: {ex.Message}";
                Debug.WriteLine($"Query execution failed: {ex}");
                Log.Logger?.Error(ex, "An exception occurred during query execution for query '{QueryName}'.", queryDefinition.Name);
            }
            finally
            {
                // Clear the decrypted password immediately after the operation (connection attempt)
                if (connectionInfo != null)
                {
                    connectionInfo.DecryptedPasswordForCurrentOperation = null;
                }
                // Ensure connection is closed and disposed
                if (connection != null)
                {
                    await connection.CloseAsync();
                    await connection.DisposeAsync();
                }
                // Dispose transaction if used
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
            stopwatch.Stop();
            overallResult.Duration = stopwatch.Elapsed;
            return overallResult;
        }

        // Helper to create DbConnection based on type
        private DbConnection CreateDbConnection(DatabaseType dbType, string connectionString)
        {
            switch (dbType)
            {
                case DatabaseType.SQLServer: return new SqlConnection(connectionString);
                case DatabaseType.PostgreSQL: return new NpgsqlConnection(connectionString);
                case DatabaseType.MySQL: return new MySqlConnection(connectionString);
                case DatabaseType.SQLite: return new SqliteConnection(connectionString);
                case DatabaseType.Oracle: return new OracleConnection(connectionString);
                default: throw new NotSupportedException($"Database type '{dbType}' not supported for connection creation.");
            }
        }

        // Helper to map model ParameterDataType to ADO.NET DbType
        private DbType MapToDbType(ParameterDataType dataType)
        {
            switch (dataType)
            {
                case ParameterDataType.String: return DbType.String;
                case ParameterDataType.Int: return DbType.Int32;
                case ParameterDataType.Decimal: return DbType.Decimal;
                case ParameterDataType.DateTime: return DbType.DateTime;
                case ParameterDataType.Boolean: return DbType.Boolean;
                // Add more mappings as needed
                default: return DbType.Object; // Fallback
            }
        }

        // Helper method to determine if the SQL is a SELECT-like query
        private static bool IsSelectLikeQuery(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;

            // 提取前100个字符用于判断，避免处理超长字符串
            var trimmed = sql.TrimStart().Substring(0, Math.Min(100, sql.TrimStart().Length)).ToUpperInvariant();

            // 检查是否以 SELECT 或 WITH 开头，并尝试确认是否是查询语句
            if (trimmed.StartsWith("SELECT"))
                return true;

            if (trimmed.StartsWith("WITH"))
            {
                // 查找第一个非 CTE 的关键语句
                // 简单判断是否包含SELECT（未涵盖全部SQL语法边缘情况）
                return trimmed.Contains("SELECT");
            }

            return false;
        }

    }
}