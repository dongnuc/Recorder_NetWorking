using Microsoft.Extensions.DependencyInjection;

namespace WpfUI.Services
{
    public class RecorderWindowScope : IDisposable
    {
        private readonly IServiceScope _scope;
        private bool _disposed = false;

        public RecorderWindowScope(IServiceProvider rootProvider)
        {
            _scope = rootProvider.CreateScope();
        }

        public IServiceProvider ServiceProvider => _scope.ServiceProvider;

        public void Dispose()
        {
            if (!_disposed)
            {
                _scope?.Dispose();
                _disposed = true;
            }
        }
    }
}
