using System.Collections.Generic;
using Disqord;
using Disqord.Gateway;

namespace SixtyFive.Services
{
    public class Sniper
    {
        public Dictionary<(Snowflake guild, Snowflake channel), CachedUserMessage> LastDeleted { get; } = new ();
    }
}