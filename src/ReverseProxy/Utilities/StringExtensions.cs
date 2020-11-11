// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.ReverseProxy.Utilities
{
    public static class StringExtensions
    {
        private static readonly Regex _csvSplitter = new Regex("(?:^|,)(\"(?:[^\"])*\"|[^,]*)", RegexOptions.Compiled);
        public static string[] SplitCSV(this string values)
        {
            List<string> list = new List<string>();
            string curr = null;
            foreach (Match match in _csvSplitter.Matches(values))
            {        
                curr = match.Value;
                if (0 == curr.Length)
                {
                    list.Add("");
                }

                list.Add(curr.TrimStart(',').Trim());
            }
            return list.ToArray();
        }
    }
}
