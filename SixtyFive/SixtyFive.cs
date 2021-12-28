using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qmmands;
using SixtyFive.Results;
using SixtyFive.Services;

namespace SixtyFive
{
    [UsedImplicitly]
    public sealed class SixtyFive : DiscordBot
    {
        public SixtyFive
        (
            IOptions<DiscordBotConfiguration> options,
            ILogger<SixtyFive> logger,
            IServiceProvider services,
            DiscordClient client
        ) : base(options, logger, services, client)
        {
            Commands.CommandExecuted += OnExecution;
            Commands.CommandExecutionFailed += OnFailure;
        }

        protected override IEnumerable<Assembly> GetModuleAssemblies()
        {
            yield return Assembly.GetExecutingAssembly();
        }
        
        private static async ValueTask OnExecution(object sender, CommandExecutedEventArgs e)
        {
            var ctx = (CommandContext) e.Context;

            switch (e.Result)
            {
                case Ok ok:
                    await ok.Respond(ctx);
                    break;

                case Err err:
                    await err.Respond(ctx);
                    break;

                case null:
                    break;

                default:
                    throw new NotImplementedException($"{e.Result.GetType().Name} is not a supported Result!");
            }
        }

        public override async Task RunAsync(CancellationToken ct)
        {
            IEnumerable<IAsyncInitialized> services = Services.GetServices<IAsyncInitialized>();

            await Task.WhenAll(services.Select(x => x.Initialize(Services, Logger)));

            MessageDeleted += Snipe;

            await base.RunAsync(ct);
        }

        private ValueTask Snipe(object sender, MessageDeletedEventArgs e)
        {
            if (e.GuildId is not Snowflake guild_id)
                return ValueTask.CompletedTask;
            
            if (e.Message is null)
                return ValueTask.CompletedTask;

            if (e.Message.Author.Id == CurrentUser.Id)
                return ValueTask.CompletedTask;
            
            var sniper = Services.GetRequiredService<Sniper>();

            sniper.LastDeleted[(guild_id, e.ChannelId)] = e.Message;
            
            return ValueTask.CompletedTask;
        }

        public override DiscordCommandContext CreateCommandContext(IPrefix prefix, string input, IGatewayUserMessage message, CachedMessageGuildChannel channel)
        {
            IServiceScope? scope = Services.CreateScope();

            var ctx = new CommandContext(this, prefix, input, message, channel, scope);

            Services.GetRequiredService<ICommandContextAccessor>().Context = ctx;

            return ctx;
        }

        private ValueTask OnFailure(object sender, CommandExecutionFailedEventArgs e)
        {
            Logger.LogError(e.Result.Exception, "Command {CommandName} failed", e.Context.Command.Name);

            return ValueTask.CompletedTask;
        }
    }
}