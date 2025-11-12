namespace Common.Interfaces.Services
{
    public interface IResetDatabaseService
    {
        Task ResetDatabaseAsync(string sqlScriptPath);
    }
}
