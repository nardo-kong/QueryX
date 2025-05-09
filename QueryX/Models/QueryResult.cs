using System.Data; // Required for DataTable

namespace QueryX.Models // Ensure namespace matches
{
    public class QueryResult
    {
        // Indicates if the overall execution (potentially multiple statements) was successful
        public bool IsSuccess { get; set; }

        // Holds error message if IsSuccess is false
        public string? ErrorMessage { get; set; }

        // Holds the result data table for SELECT queries
        public DataTable? ResultTable { get; set; }

        // Holds the number of records affected by INSERT, UPDATE, DELETE statements
        // Could be the sum if multiple non-SELECT statements were executed
        public int? RecordsAffected { get; set; }

        // Optional: Execution time
        public TimeSpan? Duration { get; set; }

        // Constructor for success with DataTable
        public QueryResult(DataTable dataTable, TimeSpan? duration = null)
        {
            IsSuccess = true;
            ResultTable = dataTable;
            RecordsAffected = null; // Not applicable for SELECT results directly in this model
            Duration = duration;
        }

        // Constructor for success with records affected
        public QueryResult(int recordsAffected, TimeSpan? duration = null)
        {
            IsSuccess = true;
            ResultTable = null;
            RecordsAffected = recordsAffected;
            Duration = duration;
        }

        // Constructor for failure
        public QueryResult(string errorMessage)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
            ResultTable = null;
            RecordsAffected = null;
        }

        // Default constructor (implies failure or not yet run)
        public QueryResult()
        {
            IsSuccess = false;
            ErrorMessage = "Query has not been executed yet.";
        }
    }
}