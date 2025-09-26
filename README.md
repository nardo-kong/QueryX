# QueryX

A powerful database query management and execution tool supporting multiple database types including SQL Server, PostgreSQL, MySQL, SQLite, and Oracle.

## Features

### Advanced Parameter Sourcing for Lists

QueryX now supports populating List parameters from SQL queries instead of just static comma-separated values. This powerful feature allows for dynamic dropdown lists that can be populated from database tables.

#### How to Use SQL-Based Parameter Options

1. **Create a List Parameter**: In the Query Manager, add a new parameter and set its Data Type to "List"

2. **Configure SQL Source**: Instead of using the "Static Options (CSV)" field, use these new fields:
   - **SQL Query**: Enter a SELECT statement that returns the options (e.g., `SELECT CustomerID, CustomerName FROM Customers ORDER BY CustomerName`)
   - **Connection**: Choose which database connection to use for executing the query
   - **Value Column**: The column name that provides the actual parameter value (e.g., "CustomerID")
   - **Display Column**: The column name shown to the user (e.g., "CustomerName"). If not specified, uses the Value Column

3. **Load Options**: Click the "Load Parameter Options" button to test and populate the options from your SQL query

#### Example Configuration

```sql
-- SQL Query for Customer selection
SELECT CustomerID, CompanyName 
FROM Customers 
WHERE Active = 1 
ORDER BY CompanyName

-- Value Column: CustomerID
-- Display Column: CompanyName
```

This creates a dropdown showing company names but passing CustomerID values to your main query.

#### Security and Validation

- Only SELECT statements are allowed for parameter option queries
- Queries containing INSERT, UPDATE, DELETE, DROP, CREATE, or ALTER are rejected
- Connection validation ensures only valid database connections are used
- Column validation checks that specified columns exist in the query results

#### Fallback Behavior

If SQL-based options fail to load, the parameter will fall back to any static options configured in the "Static Options (CSV)" field, ensuring your queries remain functional.

## Supported Database Types

- SQL Server
- PostgreSQL  
- MySQL
- SQLite
- Oracle

## Getting Started

1. Launch QueryX
2. Configure your database connections using the Connection Manager
3. Create queries with parameters using the Query Manager
4. Execute queries and export results

## Parameter Types

QueryX supports various parameter types:
- **String**: Text input
- **Int**: Integer numbers
- **Decimal**: Decimal numbers  
- **DateTime**: Date and time picker
- **Boolean**: Checkbox
- **List**: Dropdown with static or SQL-sourced options