using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace QueryX.Models // Ensure namespace matches
{
    public class QueryResult
    {
        // Indicates if the overall execution (potentially multiple statements) was successful
        public bool IsSuccess { get; set; }

        // Holds error message if IsSuccess is false
        public string? ErrorMessage { get; set; }

        // MODIFIED: Changed from single DataTable to a List of DataTables
        public List<DataTable> ResultTables { get; set; } = new List<DataTable>();

        // Holds the number of records affected by INSERT, UPDATE, DELETE statements
        // Could be the sum if multiple non-SELECT statements were executed
        public int? RecordsAffected { get; set; }

        // Optional: Execution time
        public TimeSpan? Duration { get; set; }

        // Helper property to generate a summary of the execution
        public string Summary
        {
            get
            {
                if (!IsSuccess)
                {
                    return $"Execution failed after {Duration?.TotalSeconds:F2}s: {ErrorMessage}";
                }

                var sb = new StringBuilder();
                sb.Append($"Execution successful in {Duration?.TotalSeconds:F2}s. ");

                if (ResultTables.Any())
                {
                    sb.Append($"Returned {ResultTables.Count} result set(s). ");
                }
                if (RecordsAffected.HasValue && RecordsAffected.Value > 0)
                {
                    sb.Append($"{RecordsAffected.Value} record(s) affected.");
                }
                return sb.ToString();
            }
        }

        // Constructor for success with DataTable
        public QueryResult(DataTable dataTable, TimeSpan? duration = null)
        {
            IsSuccess = true;
            //ResultTable = dataTable;
            RecordsAffected = null; // Not applicable for SELECT results directly in this model
            Duration = duration;
        }

        // Constructor for success with records affected
        public QueryResult(int recordsAffected, TimeSpan? duration = null)
        {
            IsSuccess = true;
            //ResultTable = null;
            RecordsAffected = recordsAffected;
            Duration = duration;
        }

        // Constructor for failure
        public QueryResult(string errorMessage)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
            //ResultTable = null;
            //RecordsAffected = null;
        }

        // Default constructor for general use
        public QueryResult()
        {
            IsSuccess = true; // Assume success until an error occurs
        }
    }
}