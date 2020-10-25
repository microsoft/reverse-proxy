// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Utilities
{
    internal class ParsedMetadataEntry<T>
    {
        private readonly Parser _parser;
        private readonly string _metadataName;
        // Use a volatile field of a reference Tuple<T1, T2> type to ensure atomicity during concurrent access.
        private volatile Tuple<string, T> _value;

        public delegate bool Parser(string stringValue, out T parsedValue);

        public ParsedMetadataEntry(Parser parser, string metadataName)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _metadataName = metadataName ?? throw new ArgumentNullException(nameof(metadataName));
        }

        public T GetParsedOrDefault(ClusterConfig cluster, T defaultValue)
        {
            var currentValue = _value;
            if (cluster.Metadata != null && cluster.Metadata.TryGetValue(_metadataName, out var stringValue))
            {
                if (currentValue == null || currentValue.Item1 != stringValue)
                {
                    _value = Tuple.Create(stringValue, _parser(stringValue, out var parsedValue) ? parsedValue : defaultValue);
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
