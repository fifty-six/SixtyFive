using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Gateway;
using Disqord.Http;
using Disqord.Rest;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qmmands;
using SixtyFive.Results;
using SixtyFive.Util;

namespace SixtyFive.Modules
{
    [PublicAPI]
    [RequireBotOwner]
    public class Owner : DiscordModuleBase<CommandContext>
    {
        private static readonly IEnumerable<string> IMPORTS = new[] {
            "System",
            "System.Collections",
            "System.Text",
            "System.Threading.Tasks",
            "System.Linq",
            "System.Reflection",

            "SixtyFive",
            "SixtyFive.Util",
            "SixtyFive.Modules",
            "SixtyFive.Services",

            "Disqord",
            "Disqord.Bot",
            "Disqord.Gateway",
            "Disqord.Rest",
            "Disqord.Rest.Api.Default",
            "Disqord.Rest.Default",

            "Qmmands",

            "Microsoft.Extensions.DependencyInjection",

            "Newtonsoft.Json"
        };

        internal static readonly ScriptOptions _options = ScriptOptions.Default
                                                                       .AddReferences(AppDomain.CurrentDomain.GetAssemblies().Select(x => x.FullName))
                                                                       .WithOptimizationLevel(OptimizationLevel.Release)
                                                                       .AddImports(IMPORTS);

        [Command("quit")]
        public async Task Quit()
        {
            await Reply(new LocalMessage().WithEmbeds(new LocalEmbed().WithTitle("Quitting.")));

            await Context.GetRequiredService<IHostLifetime>().StopAsync(new CancellationTokenSource(1000).Token);
            Context.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        }

        [Command("pins")]
        public async Task<Result> MovePins(ITextChannel from, ITextChannel to)
        {
            IReadOnlyList<IUserMessage>? pins = await from.FetchPinnedMessagesAsync();

            foreach (IUserMessage pin in pins.Reverse())
            {
                LocalMessage msg = pin.CopyToEmbed(from.GuildId);

                await to.SendMessageAsync(msg);
            }

            return Ok.AsEmbed("Done.");
        }

        [Command("webhook")]
        public async Task<Result> Webhook(Snowflake user_id, [Remainder] string content)
        {
            async Task<(string name, string av)?> GetInfo(Snowflake id)
            {
                if (await Context.Guild.FetchMemberAsync(id) is var mem)
                    return (mem.Nick ?? mem.Name, mem.GetAvatarUrl());

                if (await Context.Bot.FetchUserAsync(id) is IUser usr)
                    return (usr.Name, usr.GetAvatarUrl());

                return null;
            }

            if (await GetInfo(user_id) is not (var name, var av))
                return Err.AsEmbed("User does not exist!");

            await using Stream stream = await Context.Services.GetRequiredService<HttpClient>().GetStreamAsync(av);
            await using MemoryStream mem_stream = new ();

            await stream.CopyToAsync(mem_stream);
            mem_stream.Position = 0;

            IWebhook? res;

            try
            {
                res = await Context.Bot.CreateWebhookAsync
                (
                    Context.Channel.Id,
                    name,
                    s => s.Avatar = mem_stream
                );
            }
            catch (RestApiException e) when (e.StatusCode == HttpResponseStatusCode.Forbidden)
            {
                return Err.AsEmbed("Missing permissions: Webhook!");
            }

            LocalWebhookMessage msg = new LocalWebhookMessage().WithContent(content);

            await res.ExecuteAsync(msg);
            await res.DeleteAsync();

            return Ok.Success;
        }

        [Command("jolt")]
        public Task Move(int iters, int delay, params IMember[] members) => Move(iters, delay, members.Select(x => x.Id).ToArray());

        [Command("jolt")]
        public async Task<Result> Move(int iters, int delay, params Snowflake[] members)
        {
            Snowflake[] vcs = Context.Guild.GetChannels().Values.Where(x => x is IVoiceChannel).Select(x => x.Id).ToArray();

            for (int i = 0; i < iters;)
            {
                foreach (Snowflake vc in vcs)
                {
                    foreach (Snowflake member in members)
                    {
                        await Context.Guild.ModifyMemberAsync
                        (
                            member,
                            x => x.VoiceChannelId = vc
                        );
                    }

                    i++;

                    await Task.Delay(delay);

                    if (i >= iters)
                        return new Ok();
                }

                await Task.Delay(delay);
            }

            return new Ok();
        }

        [Command("purge")]
        public Task Purge() => PurgeBot();

        [Command("purge")]
        public Task Purge(IMember user, int amount = 50) => Purge(user.Id, amount);

        [Command("purge")]
        public async Task<Result> Purge(Snowflake id, int amount = 50)
        {
            IReadOnlyList<IMessage> msgs = await Context.Bot.FetchMessagesAsync(Context.ChannelId, amount);

            await Context.Channel.DeleteMessagesAsync(msgs.Where(m => m.Author.Id == id).Select(x => x.Id));

            return new Ok();
        }

        [Command("purge_bot")]
        public async Task<Result> PurgeBot(int amount = 50)
        {
            string[] command_names = Context.Bot.Commands.GetAllCommands().Select(x => x.Name).ToArray();

            bool CommandOrBotMessage(IMessage msg)
            {
                return msg.Author.Id == Context.CurrentMember.Id || command_names.Any(x => msg.Content.StartsWith($".{x}", StringComparison.OrdinalIgnoreCase));
            }

            IReadOnlyList<IMessage> msgs = await Context.Bot.FetchMessagesAsync(Context.ChannelId, amount);

            await Context.Message.GetChannel().DeleteMessagesAsync(msgs.Where(CommandOrBotMessage).Select(x => x.Id));

            return new Ok();
        }

        [Command("purge")]
        public async Task<Result> Purge(int amount)
        {
            IReadOnlyList<IMessage> msgs = await Context.Channel.FetchMessagesAsync(amount);

            if (Context.Channel is not CachedTextChannel channel)
                return Err.AsEmbed("Not a text channel.");

            await channel.DeleteMessagesAsync(msgs.Select(x => x.Id));

            return new Ok();
        }

        [Command("eval")]
        [SuppressMessage("ReSharper.DPA", "DPA0003: Excessive memory allocations in LOH")]
        public async Task<Result> Eval([Remainder] string msg)
        {
            msg = Util.Utilities.ExtractCode(msg);

            Script<object> script = CSharpScript.Create
            (
                msg,
                _options,
                typeof(RoslynContext<Owner>)
            );

            ImmutableArray<Diagnostic> diagnostics = script.Compile();

            Diagnostic[] err = diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();

            if (err.Length != 0)
            {
                var embed = new LocalEmbed {
                    Title = "Compilation failed.",
                    Description = string.Join("\n", err.Select(x => $"({x.Location.GetLineSpan().StartLinePosition}): [{x.Id}] {x.GetMessage()}")),
                    Color = Color.Red
                };

                return new Err(embed);
            }

            Task<IUserMessage> ResponseUnchecked(string str) => Context.Channel.SendMessageAsync(new LocalMessage().WithContent(str));

            ScriptState<object> res = await script.RunAsync
            (
                new RoslynContext<Owner>
                (
                    this,
                    Context,
                    async reply => await ResponseUnchecked(reply),
                    async reply => await Response(new LocalMessage().WithEmbeds(reply))
                ),
                _ => true
            );

            if (res.Exception != null)
            {
                return Err.AsCodeBlock(res.Exception.ToString(), "cs");
            }

            return res.ReturnValue != null
                ? new Ok(res.ReturnValue.Inspect())
                : new Ok();
        }
    }
}