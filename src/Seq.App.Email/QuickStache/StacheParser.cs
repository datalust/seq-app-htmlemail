using System.Collections.Generic;
using Sprache;

namespace Seq.App.Email.QuickStache
{
    static class StacheParser
    {
        public static IEnumerable<TemplateToken> ParseStache(string template)
        {
            return Template.Parse(template);
        }

        static readonly Parser<TemplateToken> Text =  
            Parse.CharExcept('{').Select(c => new[] { c }).Text()
            .Or(Parse.Char('{').Then(c => Parse.CharExcept('{').Then(d => Parse.Return(new string(new[] { c, d })))))
            .Select(s => new TextTemplateToken(s));

        static readonly Parser<TemplateToken> Identifier =
            from open in Parse.String("{{")
            from prefix in Parse.String("@").Or(Parse.Return("")).Text()
            from content in Parse.LetterOrDigit.AtLeastOnce().Text()
            from close in Parse.String("}}")
            select new IdentifierTemplateToken(prefix == "@", content);

        static readonly Parser<TemplateToken> Garbage =
            Parse.String("{").Text().Select(t => new TextTemplateToken(t));

        static readonly Parser<TemplateToken> Token = Text.Or(Identifier).Or(Garbage);

        static readonly Parser<IEnumerable<TemplateToken>> Template = Token.Many().End();
    }
}
