using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Disqord;
using JetBrains.Annotations;

namespace SixtyFive.Util
{
    [PublicAPI]
    public static class Inspector
    {
        public enum InspectionType
        {
            Fields,
            Properties
        }

        public static LocalEmbed Inspect(this object result, InspectionType memberType = InspectionType.Fields)
        {
            var eb = new LocalEmbed();

            switch (result)
            {
                case null:
                    return eb.WithDescription("null");

                case string str:
                    return eb.WithDescription(str);
            }

            Type rType = result.GetType();

            eb.Title = $"[{rType}]";

            eb.AddField("ToString", result.ToString());

            eb.AddField(Repr(result, "Repr"));

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            FieldInfo[] fields = rType.GetFields(flags).OrderBy(x => x.Name).ToArray();
            PropertyInfo[] properties = rType.GetProperties(flags).OrderBy(x => x.Name).ToArray();

            if (fields.Length == 0)
                memberType = InspectionType.Properties;

            if (properties.Length == 0)
                memberType = InspectionType.Fields;

            switch (memberType)
            {
                case InspectionType.Fields:
                {
                    foreach (FieldInfo field in fields)
                        AppendMemberInfo(result, field, eb);

                    break;
                }

                case InspectionType.Properties:
                {
                    foreach (PropertyInfo property in properties)
                        AppendMemberInfo(result, property, eb);

                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(memberType), memberType, null);
            }

            return eb;
        }

        private static void AppendMemberInfo(object result, MemberInfo member, LocalEmbed eb)
        {
            object? value = null;

            try
            {
                switch (member)
                {
                    case PropertyInfo p:
                    {
                        // Indexer propertty
                        if (p.GetIndexParameters().Length != 0)
                            return;

                        value = p.GetValue(result, null);
                        break;
                    }

                    case FieldInfo fi:
                    {
                        value = fi.GetValue(result);
                        break;
                    }
                }
            }
            catch (TargetInvocationException)
            {
                // yeet 
            }

            LocalEmbedField lefb = Repr(value, member.Name);

            eb.AddField(lefb);
        }

        private static LocalEmbedField Repr(object? value, string name)
        {
            var lefb = new LocalEmbedField
            {
                Name = name
            };

            switch (value)
            {
                case string s:
                    lefb.Value = s;

                    break;

                case IEnumerable e:
                    const int MAX_AMOUNT = 15;
                    
                    IEnumerable<object> collection = e.Cast<object>();

                    // Don't have multiple enumerations
                    IEnumerable<object> enumerated = collection as object[] ?? collection.ToArray();

                    int count = enumerated.Count();

                    Type? type = enumerated.FirstOrDefault()?.GetType();

                    if ((type?.IsPrimitive ?? false) || type == typeof(string) || type?.GetMethod("ToString", new Type[0])?.DeclaringType != typeof(object))
                    {
                        lefb.Value = "[";

                        string[] items = enumerated.Take(Math.Min(MAX_AMOUNT, count)).Select(x => x?.ToString() ?? "null").ToArray();

                        string sep = ", ";

                        if (items.Sum(x => x.Length) > 20)
                            sep = ",\n";

                        lefb.Value += string.Join(sep, items);
                        
                        if (count > MAX_AMOUNT)
                            lefb.Value += ", ...";

                        lefb.Value += "]";
                    }
                    else
                    {
                        lefb.Value = $"Item Count: {count}";
                    }

                    break;

                default:
                    lefb.Value = value?.ToString() ?? "null";
                    break;
            }

            return lefb;
        }
    }
}