// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;

namespace Yarp.Kubernetes.Protocol;

public class ReceiverOptions
{
    public Uri ControllerUrl { get; set; }

    public TimeSpan ClientTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public HttpMessageHandler ClientHttpHandler { get; set; }

    public HttpClient Client { get; set; } = default!;
}
