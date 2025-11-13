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

## Testing / Cách Test

### Cách 1: Sử dụng trong code của bạn

```csharp
using DatabaseServices.Services;
using Common.Interfaces.Services;

// 1. Chuẩn bị connection string (thay đổi theo database của bạn)
string connectionString = "Server=localhost;Database=master;Integrated Security=true;";

// 2. Đường dẫn đến file SQL
string sqlScriptPath = @"D:\CapstoneProject\middlewareTool\PRN222-Project-sample-pe-sp25\PE_PRN222_GivenSolution1\PE_PRN222_sp25.sql";

// 3. Khởi tạo service và thực thi
IDatabaseServices resetService = new ResetDatabaseService(connectionString);
await resetService.ResetDatabaseAsync(sqlScriptPath);
```

### Cách 2: Chạy file example

Xem file `Examples/ResetDatabaseExample.cs` để có ví dụ đầy đủ với error handling.

### Lưu ý quan trọng:

1. **Connection String**: 
   - Sử dụng Windows Authentication: `Server=localhost;Database=master;Integrated Security=true;`
   - Hoặc SQL Server Authentication: `Server=localhost;Database=master;User Id=sa;Password=YourPassword;`

2. **File SQL Script**:
   - Phải tồn tại tại đường dẫn chỉ định
   - Sử dụng `GO` để phân tách các batch commands
   - Nên kết nối đến database `master` để có thể DROP/CREATE database

3. **Quyền truy cập**:
   - User cần có quyền CREATE/DROP DATABASE
   - Đảm bảo SQL Server đang chạy và có thể kết nối
