using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Qmmands;
using SixtyFive.Results;
using SixtyFive.Services;
using SixtyFive.Util;

namespace SixtyFive.Modules
{
    [UsedImplicitly]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [SuppressMessage("ReSharper", "CA1822")]
    public class Misc : DiscordModuleBase<CommandContext>
    {
        [Command("ping")]
        public Result Ping()
        {
            return new Ok("i'm gonna end it all");
        }

        [Command("remind")]
        public async Task<Result> Remind(string time) 
        {
            if (!TimeSpan.TryParse(time, out var ts))
                return Err.AsEmbed("Unable to parse time!");

            if (ts.TotalMilliseconds < 0) {
                return Err.AsEmbed("nope");
            }

            await using (Context.BeginYield()) 
            {
                await Task.Delay((int) ts.TotalMilliseconds);

                var msg = new LocalMessage()
                  .WithContent("made you look")
                  .WithAllowedMentions(
                        new LocalAllowedMentions()
                          .WithMentionRepliedUser()
                  );

                await Reply(msg);
            }

            return new Ok();
        }

        [Command("choice", "choose", "rng", "rnd")]
        public Result Choice(params string[] args) {
            var rnd = Context.GetRequiredService<Random>();

            return Ok.AsEmbed(args[rnd.Next(args.Length)]);
        }

        [Command("help")]
        public Result Help()
        {
            // ReSharper disable once SuggestVarOrType_SimpleTypes
            var leb = new LocalEmbed().WithTitle("Commands");

            foreach (Command c in Context.Bot.Commands.GetAllCommands())
            {
                leb.AddField(c.Name, c.Parameters.Count != 0 ? string.Join(", ", c.Parameters.Select(x => $"{x.Name} : {x.Type.Name}")) : "_ _");
            }

            return new Ok(leb);
        }

        private static readonly Snowflake PRIME_GUILD = 547707888399810581;

        [Command("maggotprime")]
        public async Task MaggotPrime(ulong id)
        {
            IGuild guild = await Context.Bot.FetchGuildAsync(PRIME_GUILD);

            IMessage msg = await Context.Channel.FetchMessageAsync(new Snowflake(id));
            
            IEnumerable<IGuildEmoji> emotes = (await guild.FetchEmojisAsync()).Take(20);

            foreach (IGuildEmoji emoji in emotes)
            {
                try
                {
                    await msg.AddReactionAsync(LocalEmoji.FromEmoji(emoji));
                }
                // Out of reactions or don't have permissions to react.
                catch (RestApiException e) when (e.StatusCode == HttpResponseStatusCode.Forbidden)
                {
                    return;
                }
            }
        }

        [Command("snipe")]
        public Result Snipe()
        {
            var sniper = Context.GetRequiredService<Sniper>();

            if (!sniper.LastDeleted.TryGetValue((Context.GuildId, Context.ChannelId), out CachedUserMessage? msg))
                return Ok.AsEmbed("No last message saved.");

            LocalMessage copy = msg.CopyToEmbed(null, Util.Utilities.Copy.IgnoreAttachments);

            return new Ok(copy);
        }

        [Command("help")]
        public Result Help([Remainder] string command)
        {
            Command? cmd = Context.Bot.Commands.GetAllCommands().FirstOrDefault(x => x.Name.Equals(command, StringComparison.OrdinalIgnoreCase));

            if (cmd is null)
            {
                return Err.AsEmbed("Command does not exist!");
            }

            var leb = new LocalEmbed
            {
                Title = cmd.Name,
            };

            if (!string.IsNullOrEmpty(cmd.Description))
                leb.Description = cmd.Description;

            foreach (Parameter param in cmd.Parameters)
            {
                leb.AddField
                (
                    param.Name,
                    string.IsNullOrEmpty(param.Description)
                        ? param.Type.Name
                        : param.Description
                );
            }

            return new Ok(leb);
        }

        [Command("repo")]
        public async Task<Result> ShowRepo(string owner, string repo)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var req = new HttpRequestMessage
            {
                RequestUri = new Uri($"https://api.github.com/repos/{owner}/{repo}"),
                Method = HttpMethod.Get,
            };

            req.Headers.Add("Accept", "application/vnd.github.v3+json");
            req.Headers.Add("User-Agent", "fifty-six");

            HttpResponseMessage res = await Context.HttpClient.SendAsync(req, cts.Token);

            string content = await res.Content.ReadAsStringAsync(cts.Token);

            JObject json = JObject.Parse(content);

            var eb = new LocalEmbed
            {
                Title = $"{owner}/{repo}",
                Author = new LocalEmbedAuthor { 
                    Name = owner,
                    IconUrl = json["owner"]?.Value<string>("avatar_url")
                }
            };

            eb.AddField("Stars", json["stargazers_count"]);
            eb.AddField("Forks", json["forks_count"]);
            eb.AddField("Watchers", json["watchers_count"]);
            eb.AddField("Updated at", json.Value<string>("updated_at") is string s ? DateTime.Parse(s) : "Unknown");
            eb.AddField("License", json["license"]?.HasValues ?? false ? json["license"]!.Value<string>("name") : "None.");

            return new Ok(eb);
        }

        [Command("link")]
        public Result LinkEmote(ICustomEmoji emote)
        {
            string url = Discord.Cdn.GetCustomEmojiUrl(emote.Id, CdnAssetFormat.Automatic);

            var builder = new LocalEmbed
            {
                Title = emote.Name,
                ImageUrl = url,
                Description = $"[Link]({url})"
            };

            return new Ok(builder);
        }
    }
}
