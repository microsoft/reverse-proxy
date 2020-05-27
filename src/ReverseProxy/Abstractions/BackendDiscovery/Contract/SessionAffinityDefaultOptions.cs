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
        private string _defaultMissingDestinationHandler = SessionAffinityBuiltIns.MissingDestinationHandlers.ReturnError;

        /// <summary>
        /// Default session affinity mode to be used when none is specified for a backend.
        /// </summary>
        public string DefaultMode
        {
            get => _defaultMode;
            set => _defaultMode = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Default strategy handling missing destination for an affinitized request.
        /// </summary>
        public string MissingDestinationHandler
        {
            get => _defaultMissingDestinationHandler;
            set => _defaultMissingDestinationHandler = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// If set to <see cref="true"/> enables session affinity for all backends using the default settings
        /// which can be ovewritten by backend's configuration section.
        /// </summary>
        public bool EnabledForAllBackends { get; set; }
    }
}
