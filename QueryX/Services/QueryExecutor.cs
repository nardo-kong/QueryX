using QueryX.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common; // Base classes for ADO.NET
using System.Diagnostics; // For Stopwatch
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        public QueryExecutor(DatabaseService databaseService, SqlParser sqlParser)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _sqlParser = sqlParser ?? throw new ArgumentNullException(nameof(sqlParser));
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
            Stopwatch stopwatch = Stopwatch.StartNew();
            DbConnection? connection = null;
            DbTransaction? transaction = null; // Optional: Handle transactions if needed across multiple statements
            DataTable? lastResultTable = null;
            int totalRecordsAffected = 0;
            bool isSelectQuery = false;

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

                foreach (string sqlTemplate in queryDefinition.SqlTemplates)
                {
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
                        isSelectQuery = sqlTemplate.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
                        Debug.WriteLine($"Executing SQL: {sqlTemplate} with parameters: {string.Join(", ", command.Parameters.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value}"))}");

                        if (isSelectQuery)
                        {
                            using (DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
                            {
                                var dataTable = new DataTable();
                                dataTable.Load(reader); // Load reader directly into DataTable
                                lastResultTable = dataTable; // Store the result table (overwrite previous if multiple SELECTs)
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

                stopwatch.Stop();

                // Return appropriate result based on last executed statement type
                if (lastResultTable != null)
                {
                    return new QueryResult(lastResultTable, stopwatch.Elapsed);
                }
                else
                {
                    return new QueryResult(totalRecordsAffected, stopwatch.Elapsed);
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                // Optional: Rollback transaction if used
                // if (transaction != null) await transaction.RollbackAsync();
                return new QueryResult("Query execution was cancelled.");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Debug.WriteLine($"Query execution failed: {ex}");
                // Optional: Rollback transaction if used
                // if (transaction != null) try { await transaction.RollbackAsync(); } catch { /* ignore rollback error */ }
                return new QueryResult($"Execution failed: {ex.Message}");
            }
            finally
            {
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
    }
}