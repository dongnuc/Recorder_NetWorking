# Common Services

## ResetDatabaseService

### Overview
The `ResetDatabaseService` provides functionality to reset (drop and recreate) a database from SQL script files.

### Interface
```csharp
public interface IResetDatabaseService
{
    Task ResetDatabaseAsync(string sqlScriptPath);
}
```

### Usage Example

```csharp
using Common.Services;
using Common.Interfaces.Services;

// Initialize the service with a connection string
string connectionString = "Server=localhost;Database=master;Integrated Security=true;";
IResetDatabaseService resetService = new ResetDatabaseService(connectionString);

// Execute a SQL reset script
string sqlScriptPath = @"C:\Scripts\ResetDatabase.sql";
await resetService.ResetDatabaseAsync(sqlScriptPath);
```

### Features

- **File Validation**: Validates that the SQL script file exists before execution
- **Content Validation**: Checks that the SQL script is not empty
- **Batch Execution**: Automatically splits SQL scripts by `GO` statements for proper execution
- **Async Operations**: All I/O and database operations are asynchronous
- **Error Handling**: Provides clear exceptions for various error conditions
- **Timeout Configuration**: Uses a 5-minute timeout for long-running database operations

### SQL Script Format

The service supports standard SQL Server scripts with `GO` batch separators:

```sql
-- Drop existing database
DROP DATABASE IF EXISTS MyDatabase;
GO

-- Create new database
CREATE DATABASE MyDatabase;
GO

-- Use the database
USE MyDatabase;
GO

-- Create tables
CREATE TABLE Users (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL
);
GO
```

### Connection String

The connection string is provided via the constructor and should include:
- Server address
- Initial database (typically "master" for database creation/deletion)
- Authentication credentials (Windows Authentication or SQL Server Authentication)

Example connection strings:
```
// Windows Authentication
Server=localhost;Database=master;Integrated Security=true;

// SQL Server Authentication
Server=localhost;Database=master;User Id=sa;Password=YourPassword;
```

### Error Handling

The service throws the following exceptions:
- `ArgumentNullException`: When connection string is null
- `ArgumentException`: When SQL script path is null or empty
- `FileNotFoundException`: When SQL script file doesn't exist
- `InvalidOperationException`: When SQL script file is empty
- `SqlException`: When database operations fail

### Notes

- The service is designed for SQL Server databases using `Microsoft.Data.SqlClient`
- The caller is responsible for managing the connection string securely
- The SQL script should handle its own DROP and CREATE logic
- All batches in the script are executed sequentially
