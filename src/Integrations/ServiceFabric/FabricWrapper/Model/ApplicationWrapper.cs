// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <summary>
    /// TODO .
    /// </summary>
    internal class ApplicationWrapper
    {
        public Uri ApplicationName { get; set; }

        public string ApplicationTypeName { get; set; }

        public string ApplicationTypeVersion { get; set; }

        public IDictionary<string, string> ApplicationParameters { get; set; }
    }
}
