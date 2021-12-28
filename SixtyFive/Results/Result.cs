using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Rest;
using Qmmands;

namespace SixtyFive.Results
{
    public abstract class Result : CommandResult {}

    public abstract class Result<T> : Result where T : Result<T>, new()
    {
        private readonly object? _result;

        protected Result(string s) => _result = s;

        // ReSharper disable once SuggestBaseTypeForParameterInConstructor
        public Result(LocalEmbed b) => _result = b;

        // ReSharper disable once SuggestBaseTypeForParameterInConstructor
        public Result(LocalMessage msg) => _result = msg;

        public Result() {}

        public static T AsCodeBlock(string s, string? highlight = null)
        {
            return Activator.CreateInstance
            (
                typeof(T),
                $"```{highlight ?? string.Empty}\n" + s + "```"
            ) as T ?? throw new InvalidOperationException();
        }

        public static T AsEmbed(string s)
        {
            return Activator.CreateInstance
            (
                typeof(T),
                s.Length > 256
                    ? new LocalEmbed().WithDescription(s) 
                    : new LocalEmbed().WithTitle(s)
            ) as T ?? throw new InvalidOperationException();
        }

        public async Task Respond(CommandContext ctx)
        {
            switch (_result)
            {
                case null:
                    return;
                
                case string s:
                    await ctx.Bot.SendMessageAsync(ctx.ChannelId, new LocalMessage().WithContent(s));
                    break;

                case LocalEmbed emb:
                    await ctx.Bot.SendMessageAsync(ctx.ChannelId, new LocalMessage().WithEmbeds(emb));
                    break;
                
                case LocalMessage msg:
                    await ctx.Bot.SendMessageAsync(ctx.ChannelId, msg);
                    break;

                default:
                    throw new NotImplementedException($"Response of type {_result.GetType().Name} is not implemented!");
            }
        }
    }
}