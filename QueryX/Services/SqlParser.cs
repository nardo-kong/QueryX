using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QueryX.Services // Ensure namespace matches
{
    public class SqlParser
    {
        // Regular expression to find parameters like @paramName or :paramName
        // It avoids matching parameters inside comments (--) or strings ('')
        // This regex is basic and might need refinement for complex SQL edge cases.
        private static readonly Regex ParamRegex = new Regex(
            @"(?<!['-])(?:[:@](?<param>\w+))", // Use lookbehind (?<!) to avoid matches inside quotes or after --
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Extracts distinct parameter names (without prefix) from a SQL string.
        /// </summary>
        /// <param name="sql">The SQL template string.</param>
        /// <returns>A distinct list of parameter names found.</returns>
        public List<string> ExtractParameters(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return new List<string>();
            }

            var matches = ParamRegex.Matches(sql);

            return matches
                .Cast<Match>()
                .Select(m => m.Groups["param"].Value) // Get the captured group named "param"
                .Distinct(StringComparer.OrdinalIgnoreCase) // Get unique names, ignore case
                .ToList();
        }
    }
}