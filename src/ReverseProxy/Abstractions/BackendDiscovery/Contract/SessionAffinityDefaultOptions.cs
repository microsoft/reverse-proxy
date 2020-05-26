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
        private string _defaultMode = "Cookie";
        private string _defaultMissingDestinationHandler = "ReturnError";

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
    }
}
