using Common.Interfaces.Services;
using Common.Logging;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;

namespace DatabaseServices.Services
{
    public class ResetDatabaseService : IDatabaseServices
    {
        private readonly string _connectionString;
        private static readonly Regex GoRegex = new("^\\s*GO\\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);


        public ResetDatabaseService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        private string BuildMasterConnectionString(SqlConnectionStringBuilder builder)
        {
            var masterBuilder = new SqlConnectionStringBuilder(builder.ConnectionString)
            {
                InitialCatalog = "master"
            };
            return masterBuilder.ConnectionString;
        }
        private static bool ScriptContainsDatabaseManagement(string script, string databaseName)
        {
            // Check if the script contains DROP DATABASE, CREATE DATABASE, or USE commands
            // This indicates the script manages the database lifecycle itself

            // Patterns to check (case-insensitive):
            // - DROP DATABASE [DatabaseName] or DROP DATABASE DatabaseName
            // - CREATE DATABASE [DatabaseName] or CREATE DATABASE DatabaseName
            // - USE [DatabaseName] or USE DatabaseName

            var scriptUpper = script.ToUpperInvariant();
            var dbNameUpper = databaseName.ToUpperInvariant();
            var dbNameBracketed = $"[{dbNameUpper}]";

            // Check for DROP DATABASE
            if (scriptUpper.Contains($"DROP DATABASE {dbNameBracketed}") ||
                scriptUpper.Contains($"DROP DATABASE [{dbNameUpper}]") ||
                Regex.IsMatch(scriptUpper, $@"\bDROP\s+DATABASE\s+{Regex.Escape(dbNameUpper)}\b"))
            {
                return true;
            }

            // Check for CREATE DATABASE
            if (scriptUpper.Contains($"CREATE DATABASE {dbNameBracketed}") ||
                scriptUpper.Contains($"CREATE DATABASE [{dbNameUpper}]") ||
                Regex.IsMatch(scriptUpper, $@"\bCREATE\s+DATABASE\s+{Regex.Escape(dbNameUpper)}\b"))
            {
                return true;
            }

            // Check for USE
            if (scriptUpper.Contains($"USE {dbNameBracketed}") ||
                scriptUpper.Contains($"USE [{dbNameUpper}]") ||
                Regex.IsMatch(scriptUpper, $@"\bUSE\s+{Regex.Escape(dbNameUpper)}\b"))
            {
                return true;
            }

            return false;
        }

        public async Task DropDatabaseAsync(SqlConnectionStringBuilder builder, string databaseName, CancellationToken cts)
        {
            var masterConnection = BuildMasterConnectionString(builder);
            SqlConnection.ClearPool(new SqlConnection(masterConnection));

            using var connection = new SqlConnection(masterConnection);
            await connection.OpenAsync(cts);

            var commandText = $@"
                IF EXISTS (SELECT name FROM sys.databases WHERE name = @name)
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END";

            using var command = new SqlCommand(commandText, connection)
            {
                CommandType = System.Data.CommandType.Text,
            };

            command.Parameters.AddWithValue("@name",databaseName);
            await command.ExecuteNonQueryAsync(cts);
        }
        private async System.Threading.Tasks.Task ExecuteScriptFromMasterAsync(SqlConnectionStringBuilder builder, string scriptPath, System.Threading.CancellationToken ct)
        {
            // Execute the entire script from the master database context
            // This allows the script to manage database drop/create/use operations itself

            var script = await File.ReadAllTextAsync(scriptPath, ct);
            var batches = SplitSqlScript(script);

            // Connect to master database
            var masterConnectionString = BuildMasterConnectionString(builder);

            // Clear any connection pool for this connection string to avoid stale connections
            SqlConnection.ClearPool(new SqlConnection(masterConnectionString));

            using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync(ct);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }
                    using var command = new SqlCommand(batch, connection)
                {
                    CommandType = CommandType.Text
                };

                await command.ExecuteNonQueryAsync(ct);
            }
        }
        private async System.Threading.Tasks.Task ApplyScriptAsync(SqlConnectionStringBuilder builder, string databaseName, string scriptPath, System.Threading.CancellationToken ct)
        {
            var script = await File.ReadAllTextAsync(scriptPath, ct);
            var batches = SplitSqlScript(script);

            builder.InitialCatalog = databaseName;
            var connectionString = builder.ConnectionString;

            // Clear any connection pool for this connection string to avoid stale connections
            SqlConnection.ClearPool(new SqlConnection(connectionString));

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var command = new SqlCommand(batch, connection)
                {
                    CommandType = CommandType.Text
                };

                await command.ExecuteNonQueryAsync(ct);
            }
        }
        private async System.Threading.Tasks.Task CreateDatabaseAsync(SqlConnectionStringBuilder builder, string databaseName, System.Threading.CancellationToken ct)
        {
            // Clear any connection pool for this connection string to avoid stale connections
            var masterConnectionString = BuildMasterConnectionString(builder);
            SqlConnection.ClearPool(new SqlConnection(masterConnectionString));

            using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync(ct);

            using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection)
            {
                CommandType = CommandType.Text
            };

            await command.ExecuteNonQueryAsync(ct);
        }

        // main method
        public async Task<bool> ExecuteSqlWithConnectionString(string connectionString,string sqlPath,CancellationToken cts)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);

                if (string.IsNullOrEmpty(builder.InitialCatalog))
                {
                    LogManager.Instance.LogError("Connection string don't contain name database");
                    return false;
                }

                var databaseName = builder.InitialCatalog;


                var script = await File.ReadAllTextAsync(sqlPath);
                var scriptManagesDatabase = ScriptContainsDatabaseManagement(script, databaseName);

                if (scriptManagesDatabase)
                {
                    await ExecuteScriptFromMasterAsync(builder, sqlPath, cts);
                    return true;
                }
                else
                {
                    LogManager.Instance.LogWarning("Scrip path not contain create/drop");

                    await DropDatabaseAsync(builder, databaseName,cts);

                    await CreateDatabaseAsync(builder, databaseName, cts);
                    
                    await ApplyScriptAsync(builder, databaseName,sqlPath, cts);
                    return true;
                }
            }
            catch (Exception ex)
            {

            }


            return false;
        }


        public async Task ResetDatabaseAsync(string sqlScriptPath)
        {
            if (string.IsNullOrWhiteSpace(sqlScriptPath))
            {
                throw new ArgumentException("SQL script path cannot be null or empty.", nameof(sqlScriptPath));
            }

            if (!File.Exists(sqlScriptPath))
            {
                throw new FileNotFoundException($"SQL script file not found at path: {sqlScriptPath}");
            }

            // Read the SQL script content from the file
            string sqlScript = await File.ReadAllTextAsync(sqlScriptPath);

            if (string.IsNullOrWhiteSpace(sqlScript))
            {
                throw new InvalidOperationException($"SQL script file is empty: {sqlScriptPath}");
            }

            // Execute the SQL script
            await ExecuteSqlScriptAsync(sqlScript);
        }

        private async Task ExecuteSqlScriptAsync(string sqlScript)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Split the script by GO statements (SQL Server batch separator)
            var batches = SplitSqlScript(sqlScript);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                using var command = new SqlCommand(batch, connection);
                command.CommandTimeout = 300; // 5 minutes timeout for long-running operations
                await command.ExecuteNonQueryAsync();
            }
        }

        private static IEnumerable<string> SplitSqlScript(string sqlScript)
        {
            // Split by GO statements (case-insensitive)
            // GO must be on its own line
            var lines = sqlScript.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var currentBatch = new List<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Check if the line is a GO statement
                if (trimmedLine.Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentBatch.Count > 0)
                    {
                        yield return string.Join(Environment.NewLine, currentBatch);
                        currentBatch.Clear();
                    }
                }
                else
                {
                    currentBatch.Add(line);
                }
            }

            // Return the last batch if there's any remaining content
            if (currentBatch.Count > 0)
            {
                yield return string.Join(Environment.NewLine, currentBatch);
            }
        }




    }
}
