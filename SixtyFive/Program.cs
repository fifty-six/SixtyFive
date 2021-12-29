using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Gateway;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixtyFive.Services;
using Disqord.Bot.Hosting;
using Microsoft.Extensions.Hosting;

namespace SixtyFive
{
    public static class Program
    {
        private static readonly string ConfigRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);

        private static string GetOrCreateConfigPath()
        {
            string path = Path.GetFullPath(Path.Combine(ConfigRoot, "SixtyFive"));

            if (Directory.Exists(path))
                return path;

            Directory.CreateDirectory(path);

            return path;
        }

        public static async Task Main()
        {
            using IHost? host = new HostBuilder()
                                .ConfigureServices(ConfigureServices)
                                .ConfigureLogging
                                (
                                    l =>
                                    {
                                        l.AddDebug();
                                        l.AddConsole();
                                        l.SetMinimumLevel(LogLevel.Debug);
                                    }
                                )
                                .ConfigureAppConfiguration
                                (
                                    x => x.SetBasePath(GetOrCreateConfigPath())
                                          .AddJsonFile("config.json")
                                )
                                .ConfigureDiscordBot<SixtyFive>
                                (
                                    (ctx, bot) =>
                                    {
                                        bot.Activities = new[] { new LocalActivity("the screams of children", ActivityType.Listening) };
                                        bot.Token = ctx.Configuration["Token"];
                                        bot.Prefixes = new[] { "." };
                                    }
                                )
                                .Build();

            var cts = new CancellationTokenSource();
            
            await host.RunAsync(cts.Token);
        }

        private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection scol) =>
            scol
                .AddSingleton<HttpClient>()
                .AddSingleton<TcpClient>()
                .AddSingleton<Godbolt>()
                .AddSingleton<Sniper>()
                .AddSingleton<IAsyncInitialized>(x => x.GetRequiredService<Godbolt>())
                .BuildServiceProvider();
    }
}
