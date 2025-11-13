namespace Common.Interfaces.Services
{
    public interface IDatabaseServices
    {
        Task ResetDatabaseAsync(string sqlScriptPath);
    }
}
