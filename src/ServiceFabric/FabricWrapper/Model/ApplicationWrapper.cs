// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.ServiceFabric;

/// <summary>
/// TODO .
/// </summary>
internal sealed class ApplicationWrapper
{
    public Uri ApplicationName { get; set; }

    public string ApplicationTypeName { get; set; }

    public string ApplicationTypeVersion { get; set; }

    public IDictionary<string, string> ApplicationParameters { get; set; }
}
