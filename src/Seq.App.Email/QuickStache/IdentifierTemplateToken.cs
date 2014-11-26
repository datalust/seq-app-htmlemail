using System.Collections;
using System.Collections.Generic;
using System.IO;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Email.QuickStache
{
    class IdentifierTemplateToken : TemplateToken
    {
        readonly bool _isPrefixed;
        readonly string _identifier;

        public IdentifierTemplateToken(bool isPrefixed, string identifier)
        {
            _isPrefixed = isPrefixed;
            _identifier = identifier;
        }

        public override void Render(TextWriter output, Event<LogEventData> evt)
        {
            object value = null;
            if (_isPrefixed)
            {
                if (_identifier == "Timestamp")
                    value = evt.Data.LocalTimestamp;
                else if (_identifier == "Id")
                    value = evt.Data.Id;
                else if (_identifier == "Level")
                    value = evt.Data.Level;
                else if (_identifier == "RenderedMessage")
                    value = evt.Data.RenderedMessage;
                else if (_identifier == "Exception")
                    value = evt.Data.Exception;
                else
                {
                    int unused;
                    if (int.TryParse(_identifier, out unused) && evt.Data.Properties != null)
                        evt.Data.Properties.TryGetValue(_identifier, out value);
                }
            }
            else
            {
                if (evt.Data.Properties != null)
                    evt.Data.Properties.TryGetValue(_identifier, out value);
            }

            RenderValue(output, value);
        }

        void RenderValue(TextWriter output, object value)
        {
            if (value != null)
            {
                if (!(value is string))
                {
                    var dict = value as IDictionary<string, object>;
                    if (dict != null)
                    {
                        output.Write("{");
                        var delim = "";

                        if (dict.Count > 0)
                            output.Write(" ");

                        foreach (var kvp in dict)
                        {
                            output.Write(delim);
                            output.Write(kvp.Key);
                            output.Write(" = ");
                            RenderValue(output, kvp.Value);
                            delim = ", ";
                        }

                        if (dict.Count > 0)
                            output.Write(" ");

                        output.Write("}");
                        return;
                    }

                    var elems = value as IEnumerable;
                    if (elems != null)
                    {
                        output.Write("[");
                        var delim = "";

                        foreach (var elem in elems)
                        {
                            output.Write(delim);
                            RenderValue(output, elem);
                            delim = ", ";
                        }

                        output.Write("]");
                        return;
                    }
                }
            }

            output.Write(value ?? "");
        }
    }
}