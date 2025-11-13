# DatabaseServices

## Overview
The `DatabaseServices` project provides functionality to manage database operations, including resetting (dropping and recreating) databases from SQL script files.

## Structure

```
DatabaseServices/
├── Services/
│   └── ResetDatabaseService.cs
└── DatabaseServices.csproj
```

## Services

### ResetDatabaseService

Implements `IDatabaseServices` interface from `Common.Interfaces.Services`.

**Features:**
- Reads SQL scripts from file paths
- Validates file existence and content
- Connects to SQL Server databases
- Executes SQL scripts with GO batch separation
- 5-minute timeout for long-running operations
- Comprehensive error handling

**Usage Example:**

```csharp
using DatabaseServices.Services;
using Common.Interfaces.Services;

// Initialize the service with a connection string
string connectionString = "Server=localhost;Database=master;Integrated Security=true;";
IDatabaseServices resetService = new ResetDatabaseService(connectionString);

// Execute a SQL reset script
string sqlScriptPath = @"C:\Scripts\ResetDatabase.sql";
await resetService.ResetDatabaseAsync(sqlScriptPath);
```

**SQL Script Format:**

Scripts should use standard SQL Server format with `GO` batch separators:

```sql
DROP DATABASE IF EXISTS MyDatabase;
GO

CREATE DATABASE MyDatabase;
GO

USE MyDatabase;
GO

CREATE TABLE Users (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL
);
GO
```

## Dependencies

- **Common**: Project reference for interfaces and shared models
- **Microsoft.Data.SqlClient**: Version 5.2.2 for SQL Server connectivity

## Error Handling

The service throws the following exceptions:
- `ArgumentNullException`: When connection string is null
- `ArgumentException`: When SQL script path is null or empty
- `FileNotFoundException`: When SQL script file doesn't exist
- `InvalidOperationException`: When SQL script file is empty
- `SqlException`: When database operations fail
