// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public readonly struct ClusterSessionAffinityOptions
    {
        public ClusterSessionAffinityOptions(bool enabled, string mode, string failurePolicy, IReadOnlyDictionary<string, string> settings)
        {
            Mode = mode;
            FailurePolicy = failurePolicy;
            Settings = settings;
            Enabled = enabled;
        }

        public bool Enabled { get; }

        public string Mode { get; }

        public string FailurePolicy { get; }

        public IReadOnlyDictionary<string, string> Settings { get; }
    }
}
