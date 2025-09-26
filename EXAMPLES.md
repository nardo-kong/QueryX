# QueryX Examples

This document provides examples of how to use QueryX's advanced parameter sourcing feature.

## Example 1: Customer Selection Parameter

### Setup Database Tables

First, create sample tables in your database:

```sql
-- Create a Customers table
CREATE TABLE Customers (
    CustomerID INT PRIMARY KEY,
    CompanyName NVARCHAR(100) NOT NULL,
    ContactName NVARCHAR(50),  
    Country NVARCHAR(50),
    Active BIT DEFAULT 1
);

-- Insert sample data
INSERT INTO Customers (CustomerID, CompanyName, ContactName, Country, Active) VALUES
(1, 'Alfreds Futterkiste', 'Maria Anders', 'Germany', 1),
(2, 'Ana Trujillo Emparedados', 'Ana Trujillo', 'Mexico', 1),
(3, 'Antonio Moreno Taquería', 'Antonio Moreno', 'Mexico', 1),
(4, 'Around the Horn', 'Thomas Hardy', 'UK', 1),
(5, 'Berglunds snabbköp', 'Christina Berglund', 'Sweden', 0);
```

### Configure Query with SQL-Based Parameter

1. **Create a new Query** in Query Manager:
   - Name: "Customer Orders Report"
   - Description: "Get orders for a specific customer"

2. **Add SQL Template**:
   ```sql
   SELECT o.OrderID, o.OrderDate, o.ShippedDate, c.CompanyName
   FROM Orders o
   INNER JOIN Customers c ON o.CustomerID = c.CustomerID  
   WHERE c.CustomerID = @customerId
   ORDER BY o.OrderDate DESC
   ```

3. **Configure Parameter**:
   - Placeholder: `@customerId`
   - Display Name: `Customer`
   - Data Type: `List`
   - SQL Query: `SELECT CustomerID, CompanyName FROM Customers WHERE Active = 1 ORDER BY CompanyName`
   - Connection: (Select your database connection)
   - Value Column: `CustomerID`
   - Display Column: `CompanyName`

4. **Load Options**: Click "Load Parameter Options" to test the configuration

### Result

When executing the query, users will see a dropdown with company names (like "Alfreds Futterkiste") but the actual query will receive the CustomerID value (like "1").

## Example 2: Dynamic Category Filter

### Setup

```sql
-- Create Categories table
CREATE TABLE Categories (
    CategoryID INT PRIMARY KEY,
    CategoryName NVARCHAR(50) NOT NULL,
    Description TEXT
);

INSERT INTO Categories VALUES 
(1, 'Beverages', 'Soft drinks, coffees, teas, beers, and ales'),
(2, 'Condiments', 'Sweet and savory sauces, relishes, spreads, and seasonings'),
(3, 'Dairy Products', 'Cheeses'),
(4, 'Grains/Cereals', 'Breads, crackers, pasta, and cereal');
```

### Query Configuration

- **SQL Query for Options**: `SELECT CategoryID, CategoryName FROM Categories ORDER BY CategoryName`
- **Value Column**: `CategoryID`
- **Display Column**: `CategoryName`

- **Main Query**:
   ```sql
   SELECT p.ProductName, p.UnitPrice, c.CategoryName
   FROM Products p 
   INNER JOIN Categories c ON p.CategoryID = c.CategoryID
   WHERE p.CategoryID = @categoryId
   ORDER BY p.ProductName
   ```

## Example 3: Multi-Column Display

### Advanced Option Query

```sql
-- Show customer with additional context
SELECT 
    CustomerID,
    CompanyName + ' (' + Country + ') - ' + ContactName as DisplayName
FROM Customers 
WHERE Active = 1 
ORDER BY CompanyName
```

### Configuration

- **Value Column**: `CustomerID`
- **Display Column**: `DisplayName`

This will show options like "Alfreds Futterkiste (Germany) - Maria Anders" in the dropdown while passing the CustomerID to the query.

## Example 4: Fallback to Static Options

### Configuration

You can configure both SQL-based and static options:

- **Static Options (CSV)**: `1,2,3,4,5`
- **SQL Query**: `SELECT UserID, UserName FROM Users WHERE Active = 1`

If the SQL query fails (database unavailable, query error, etc.), the parameter will fall back to the static options.

## Best Practices

1. **Always use ORDER BY** in your option queries for consistent display
2. **Filter for active/valid records only** to avoid obsolete options
3. **Keep option queries simple** - avoid complex joins or subqueries
4. **Test your configuration** using the "Load Parameter Options" button
5. **Provide fallback static options** for critical parameters
6. **Use descriptive display columns** that help users make the right choice

## Security Notes

- Only SELECT statements are allowed in parameter option queries
- Queries are validated to prevent data modification statements
- Use appropriate database permissions for QueryX connection accounts
- Consider query performance impact with large option datasets