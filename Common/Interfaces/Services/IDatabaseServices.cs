namespace Common.Interfaces.Services
{
    public interface IDatabaseServices
    {
        Task ResetDatabaseAsync(string sqlScriptPath);
        Task<bool> ExecuteSqlWithConnectionString(string connectionString, string sqlPath, CancellationToken cts);
    }
}
