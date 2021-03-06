using System;
using System.Linq;
using Disqord;

namespace SixtyFive.Util
{
    public static class Utilities
    {
        public static string ExtractCode(string code)
        {
            code = code.Trim();

            if (!code.StartsWith("```") || !code.EndsWith("```"))
                return code;

            string[] lines = code.Split('\n');

            // If we have a space in the line with the first set of backticks then the identifier isn't used for highlighting.
            int start = lines[0].Contains(" ")
                    ? 0
                    : 1
                ;

            // If the backticks are on a separate line, just cut them out
            Index end = lines[^1].Length == 3
                    ? ^1
                    : ^0
                ;

            // Have to trim the backticks because we need the identifier anyways.
            if (start == 0)
            {
                ref string line = ref lines[0];

                line = line.Substring(3, line.Length - 3);
            }

            // Otherwise, we trim ourselves.
            if (end.Equals(^0))
            {
                ref string line = ref lines[^1];

                line = line[..^3];
            }

            code = string.Join('\n', lines[start..end]);

            return code;
        }

        public enum Copy
        {
            CopyAttachments,
            IgnoreAttachments
        }

        public static LocalMessage CopyToEmbed(this IUserMessage orig, Snowflake? guild_id, Copy copy_type = Copy.CopyAttachments)
        {
            var msg = new LocalMessage();

            LocalEmbed? embed = new LocalEmbed()
                                .WithAuthor(orig.Author)
                                .WithDescription(orig.Content)
                                .WithTimestamp(orig.CreatedAt());

            if (guild_id is Snowflake id)
            {
                string? link = Discord.MessageJumpLink(id, orig.ChannelId, orig.Id);

                embed.WithFields
                (
                    new LocalEmbedField {
                        Name = "Link",
                        Value = $"[Jump!]({link})"
                    }
                );
            }

            if (copy_type != Copy.CopyAttachments)
                return msg.WithEmbeds(embed);

            if (orig.Embeds.FirstOrDefault(x => !string.IsNullOrEmpty(x.Image?.Url)) is IEmbed image)
                embed.WithImageUrl(image.Image.Url);
            else if (orig.Embeds.FirstOrDefault(x => x.Type == "image") is IEmbed url_image)
                embed.WithImageUrl(url_image.Url);

            if (!orig.Attachments.Any())
                return msg.WithEmbeds(embed);

            foreach (IAttachment attachment in orig.Attachments)
            {
                if (attachment.ContentType?.StartsWith("image/") ?? false)
                    embed.WithImageUrl(attachment.ProxyUrl);
                else
                    embed.AddField("Attachment", $"[{attachment.FileName}]({attachment.ProxyUrl})");
            }

            return msg.WithEmbeds(embed);
        }
    }
}
