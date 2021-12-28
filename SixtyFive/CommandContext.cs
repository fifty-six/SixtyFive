using System;
using System.Net.Http;
using Disqord.Bot;
using Disqord.Gateway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SixtyFive
{
    public sealed class CommandContext : DiscordGuildCommandContext, IServiceProvider
    {
        public ILogger Logger { get; }

        public new SixtyFive Bot { get; }

        public HttpClient HttpClient { get; }
        
        public CommandContext
        (
            SixtyFive bot,
            IPrefix prefix,
            string input,
            IGatewayUserMessage message,
            CachedMessageGuildChannel channel,
            IServiceScope scope
        )
            : base(bot, prefix, input, message, channel, scope)
        {
            Bot = bot;
            HttpClient = Services.GetRequiredService<HttpClient>();
            Logger = bot.Logger;
        }

        public object? GetService(Type serviceType)
        {
            return Services.GetService(serviceType);
        }
    }
}