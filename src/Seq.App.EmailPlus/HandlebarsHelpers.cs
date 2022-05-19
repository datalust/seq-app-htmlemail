using HandlebarsDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Seq.App.EmailPlus
{
    static class HandlebarsHelpers
    {
        public static void Register()
        {
            Handlebars.RegisterHelper("pretty", PrettyPrintHelper);
            Handlebars.RegisterHelper("if_eq", IfEqHelper);
            Handlebars.RegisterHelper("substring", SubstringHelper);
        }

        static void PrettyPrintHelper(EncodedTextWriter output, Context context, Arguments arguments)
        {
            var value = arguments.FirstOrDefault();
            if (value == null)
                output.WriteSafeString("null");
            else if (value is IEnumerable<object> || value is IEnumerable<KeyValuePair<string, object>>)
                output.Write(JsonConvert.SerializeObject(FromDynamic(value)));
            else
            {
                var str = value.ToString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    output.WriteSafeString("&nbsp;");
                }
                else
                {
                    output.Write(str);
                }
            }
        }

        static void IfEqHelper(EncodedTextWriter output, BlockHelperOptions options, Context context, Arguments arguments)
        {
            if (arguments.Length != 2)
            {
                options.Inverse(output, context);
                return;
            }

            var lhs = (arguments[0]?.ToString() ?? "").Trim();
            var rhs = (arguments[1]?.ToString() ?? "").Trim();

            if (lhs.Equals(rhs, StringComparison.Ordinal))
            {
                options.Template(output, context);
            }
            else
            {
                options.Inverse(output, context);
            }
        }

        static object FromDynamic(object o)
        {
            if (o is IEnumerable<KeyValuePair<string, object>> dictionary)
            {
                return dictionary.ToDictionary(kvp => kvp.Key, kvp => FromDynamic(kvp.Value));
            }

            if (o is IEnumerable<object> enumerable)
            {
                return enumerable.Select(FromDynamic).ToArray();
            }

            return o;
        }

        static object SubstringHelper(Context context, Arguments arguments)
        {
            //{{ substring value 0 30 }}
            var value = arguments.FirstOrDefault();

            if (value == null)
                return null;

            if (arguments.Length < 2)
            {
                // No start or length arguments provided
                return value;
            }

            int start;
            if (arguments.Length < 3)
            {
                // just a start position provided
                int.TryParse(arguments[1].ToString(), out start);
                if (start > value.ToString().Length)
                {
                    // start of substring after end of string.
                    return null;
                }
                
                return value.ToString().Substring(start);
            }
            
            // Start & length provided.
            int.TryParse(arguments[1].ToString(), out start);
            int.TryParse(arguments[2].ToString(), out var end);

            if (start > value.ToString().Length)
            {
                // start of substring after end of string.
                return null;
            }
            // ensure the length is still in the string to avoid ArgumentOutOfRangeException
            if (end > value.ToString().Length - start)
            {
                end = value.ToString().Length - start;
            }

            return value.ToString().Substring(start, end);
        }
    }
}
