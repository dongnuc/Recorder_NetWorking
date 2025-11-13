using Common.Interfaces.Services;
using Microsoft.Data.SqlClient;

namespace DatabaseServices.Services
{
    public class ResetDatabaseService : IDatabaseServices
    {
        private readonly string _connectionString;

        public ResetDatabaseService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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
