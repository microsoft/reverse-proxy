// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract
{
    /// <summary>
    /// Sets default values for session affinity configuration settings.
    /// </summary>
    public class SessionAffinityDefaultOptions
    {
        private string _defaultMode = SessionAffinityBuiltIns.Modes.Cookie;
        private string _defaultAffinityFailurePolicy = SessionAffinityBuiltIns.AffinityFailurePolicies.Redistribute;

        /// <summary>
        /// Default session affinity mode to be used when none is specified for a backend.
        /// </summary>
        public string DefaultMode
        {
            get => _defaultMode;
            set => _defaultMode = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Default affinity failure handling policy.
        /// </summary>
        public string AffinityFailurePolicy
        {
            get => _defaultAffinityFailurePolicy;
            set => _defaultAffinityFailurePolicy = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// If set to <see cref="true"/> enables session affinity for all backends using the default settings
        /// which can be ovewritten by backend's configuration section.
        /// </summary>
        public bool EnabledForAllBackends { get; set; }
    }
}
