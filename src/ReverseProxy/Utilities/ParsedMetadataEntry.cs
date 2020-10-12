// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Utilities
{
    internal class ParsedMetadataEntry<T>
    {
        private readonly Func<string, (T, bool)> _parser;
        // Use a volatile field of a reference Tuple<T1, T2> type to ensure atomicity during concurrent access.
        private volatile Tuple<string, T> _value;

        public ParsedMetadataEntry(Func<string, (T, bool)> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public T GetParsedOrDefault(ClusterConfig cluster, string metadataName, T defaultValue)
        {
            var currentValue = _value;
            if (cluster.Metadata != null && cluster.Metadata.TryGetValue(metadataName, out var stringValue))
            {
                if (currentValue == null || currentValue.Item1 != stringValue)
                {
                    var parserResult = _parser(stringValue);
                    _value = Tuple.Create(stringValue, parserResult.Item2 ? parserResult.Item1 : defaultValue);
                }
            }
            else if (currentValue == null || currentValue.Item1 != null)
            {
                _value = Tuple.Create((string) null, defaultValue);
            }

            return _value.Item2;
        }
    }
}
