using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SixtyFive.Services
{
    public interface IAsyncInitialized
    {
        public Task Initialize(IServiceProvider provider, ILogger logger);
    }
}