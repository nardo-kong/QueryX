using QueryX.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QueryX.Services
{
    /// <summary>
    /// Service responsible for loading parameter options from SQL queries
    /// </summary>
    public class ParameterOptionsService
    {
        private readonly DatabaseService _databaseService;
        private readonly EncryptionService _encryptionService;

        public ParameterOptionsService(DatabaseService databaseService, EncryptionService encryptionService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        /// <summary>
        /// Loads options for a parameter from its configured SQL query
        /// </summary>
        /// <param name="parameter">The parameter definition with SQL query configuration</param>
        /// <param name="connectionInfo">Database connection to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of options loaded from the database</returns>
        public async Task<List<ListOption>> LoadParameterOptionsAsync(
            ParameterDefinition parameter, 
            DatabaseConnectionInfo connectionInfo,
            CancellationToken cancellationToken = default)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (connectionInfo == null)
                throw new ArgumentNullException(nameof(connectionInfo));

            if (string.IsNullOrWhiteSpace(parameter.ListOptionsSourceQuery))
                return new List<ListOption>();

            var options = new List<ListOption>();
            DbConnection? connection = null;

            try
            {
                // Build connection string
                connectionInfo.DecryptedPasswordForCurrentOperation = 
                    _encryptionService.DecryptPassword(connectionInfo.EncryptedPassword);

                string connectionString = _databaseService.BuildConnectionString(connectionInfo);
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Failed to build connection string.");
                }

                // Create and open connection
                connection = _databaseService.CreateDbConnection(connectionInfo.DbType, connectionString);
                await connection.OpenAsync(cancellationToken);

                // Execute the query
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = parameter.ListOptionsSourceQuery;
                    
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        // Determine column strategy
                        var valueColumn = parameter.ListOptionsValueColumn ?? reader.GetName(0);
                        var displayColumn = parameter.ListOptionsDisplayColumn ?? valueColumn;

                        // Validate columns exist
                        bool hasValueColumn = HasColumn(reader, valueColumn);
                        bool hasDisplayColumn = HasColumn(reader, displayColumn);

                        if (!hasValueColumn)
                        {
                            throw new InvalidOperationException($"Value column '{valueColumn}' not found in query results.");
                        }

                        if (!hasDisplayColumn)
                        {
                            throw new InvalidOperationException($"Display column '{displayColumn}' not found in query results.");
                        }

                        // Read the data
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            var value = reader[valueColumn]?.ToString() ?? string.Empty;
                            var displayText = reader[displayColumn]?.ToString() ?? string.Empty;
                            
                            options.Add(new ListOption(value, displayText));
                        }
                    }
                }
            }
            finally
            {
                if (connection != null)
                {
                    await connection.CloseAsync();
                    await connection.DisposeAsync();
                }
                
                // Clear sensitive data
                connectionInfo.DecryptedPasswordForCurrentOperation = null;
            }

            return options;
        }

        /// <summary>
        /// Checks if a column exists in the DataReader
        /// </summary>
        private static bool HasColumn(DbDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Validates that a parameter's SQL query configuration is valid
        /// </summary>
        /// <param name="parameter">Parameter to validate</param>
        /// <returns>Validation result with any error messages</returns>
        public (bool IsValid, string? ErrorMessage) ValidateParameterConfiguration(ParameterDefinition parameter)
        {
            if (parameter == null)
                return (false, "Parameter cannot be null");

            if (parameter.DataType != ParameterDataType.List)
                return (true, null); // Not a list parameter, no validation needed

            if (!parameter.UsesSqlForOptions)
                return (true, null); // Uses static options, no validation needed

            if (string.IsNullOrWhiteSpace(parameter.ListOptionsSourceQuery))
                return (false, "SQL query is required for dynamic parameter options");

            if (parameter.ListOptionsConnectionId == null || parameter.ListOptionsConnectionId == Guid.Empty)
                return (false, "Connection is required for dynamic parameter options");

            // Basic SQL validation - check for dangerous keywords
            var query = parameter.ListOptionsSourceQuery.Trim().ToUpperInvariant();
            if (!query.StartsWith("SELECT"))
                return (false, "Query must be a SELECT statement");

            if (query.Contains("DROP") || query.Contains("DELETE") || query.Contains("UPDATE") || 
                query.Contains("INSERT") || query.Contains("CREATE") || query.Contains("ALTER"))
                return (false, "Query cannot contain data modification statements");

            return (true, null);
        }
    }
}