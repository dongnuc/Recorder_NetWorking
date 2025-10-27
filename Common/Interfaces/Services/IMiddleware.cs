namespace Common.Interfaces.Services
{
    public interface IMiddleware
    {
        //IRecorder Recorder { get; set; }
        Task StartAsync(bool useHttp);
        Task StopAsync();
        bool IsRunning { get; }
    }
}
