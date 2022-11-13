using System;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot;
using Disqord.Rest;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quartz;

namespace SixtyFive.Modules;

[UsedImplicitly]
public class RemindJob : IJob
{
    private readonly DiscordBot _db;
    private readonly ILogger<SixtyFive> _logger;

    public RemindJob(DiscordBot db, ILogger<SixtyFive> log) => (_db, _logger) = (db, log);

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var src = (Snowflake) context.MergedJobDataMap.Get("Message");
            var channel = (Snowflake) context.MergedJobDataMap.Get("Channel");

            LocalMessage? msg = new LocalMessage()
                .WithContent("made you look")
                .WithReply(src, channel)
                .WithAllowedMentions(
                    new LocalAllowedMentions().WithMentionRepliedUser()
                );

            await _db.SendMessageAsync(channel, msg);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to raise reminder!");
        }
    }
}