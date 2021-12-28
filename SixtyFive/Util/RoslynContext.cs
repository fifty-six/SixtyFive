using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using JetBrains.Annotations;

namespace SixtyFive.Util
{
    [PublicAPI]
    public class RoslynContext<T>
    {
        public RoslynContext
        (
            T self,
            DiscordGuildCommandContext context,
            Func<string, Task<IMessage>> replyAsync,
            Func<LocalEmbed, Task<IMessage>> replyEmbed
        )
        {
            Context = context;
            this.self = self;
            Reply = replyAsync;
            ReplyEmbed = replyEmbed;
        }

        public DiscordGuildCommandContext Context { get; set; }
        public T                          self    { get; set; }

        public Func<string, Task<IMessage>>     ReplyAsync => Reply;
        public Func<string, Task<IMessage>>     Reply      { get; set; }
        public Func<LocalEmbed, Task<IMessage>> ReplyEmbed { get; set; }
    }
}