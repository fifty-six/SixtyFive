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

        public static LocalMessage CopyToEmbed(this IUserMessage pin, Snowflake? guild_id, Copy copy_type = Copy.CopyAttachments)
        {
            var msg = new LocalMessage();

            LocalEmbed? embed = new LocalEmbed()
                                .WithAuthor(pin.Author)
                                .WithDescription(pin.Content)
                                .WithTimestamp(pin.CreatedAt());

            if (guild_id is Snowflake id)
            {
                string? link = Discord.MessageJumpLink(id, pin.ChannelId, pin.Id);

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

            if (pin.Embeds.FirstOrDefault(x => !string.IsNullOrEmpty(x.Image?.Url)) is Embed image)
                embed.WithImageUrl(image.Image.Url);
            else if (pin.Embeds.FirstOrDefault(x => x.Type == "image") is Embed url_image)
                embed.WithImageUrl(url_image.Url);

            if (!pin.Attachments.Any())
                return msg.WithEmbeds(embed);

            foreach (Attachment attachment in pin.Attachments)
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