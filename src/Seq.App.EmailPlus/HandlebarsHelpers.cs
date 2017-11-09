using HandlebarsDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        static void PrettyPrintHelper(TextWriter output, object context, object[] arguments)
        {
            var value = arguments.FirstOrDefault();
            if (value == null)
                output.WriteSafeString("null");
            else if (value is IEnumerable<object> || value is IEnumerable<KeyValuePair<string, object>>)
                output.WriteSafeString(JsonConvert.SerializeObject(FromDynamic(value)));
            else
                output.WriteSafeString(value.ToString());
        }

        static void IfEqHelper(TextWriter output, HelperOptions options, dynamic context, object[] arguments)
        {
            if (arguments?.Length != 2)
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
            var dictionary = o as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                return dictionary.ToDictionary(kvp => kvp.Key, kvp => FromDynamic(kvp.Value));
            }

            var enumerable = o as IEnumerable<object>;
            if (enumerable != null)
            {
                return enumerable.Select(FromDynamic).ToArray();
            }

            return o;
        }

        static void SubstringHelper(TextWriter output, object context, object[] arguments)
        {
            //{{ substring value 0 30 }}
            var value = arguments.FirstOrDefault();

            if (value == null)
            {
                output.WriteSafeString("");
                return;
            }

            int start = 0;
            int end = value.ToString().Length;

            if (arguments?.Length < 2)
            {
                // No start or length arguments provided
                output.WriteSafeString(value);
            }
            else if (arguments?.Length < 3)
            {
                // just a start position provided
                int.TryParse(arguments[1].ToString(), out start);
                if (start > value.ToString().Length)
                {
                    // start of substring after end of string.
                    output.WriteSafeString("null");
                    return;
                }
                output.WriteSafeString(value.ToString().Substring(start));
            }
            else
            {
                // Start & length provided.
                int.TryParse(arguments[1].ToString(), out start);
                int.TryParse(arguments[2].ToString(), out end);

                if (start > value.ToString().Length)
                {
                    // start of substring after end of string.
                    output.WriteSafeString("null");
                    return;
                }
                // ensure the length is still in the string to avoid ArgumentOutOfRangeException
                if (end > value.ToString().Length - start)
                {
                    end = value.ToString().Length - start;
                }

                output.WriteSafeString(value.ToString().Substring(start, end));
            }
        }
    }
}
