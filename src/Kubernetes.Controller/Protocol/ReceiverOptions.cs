// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;

namespace Yarp.Kubernetes.Protocol;

public class ReceiverOptions
{
    public Uri ControllerUrl { get; set; }
    
    public HttpMessageInvoker Client { get; set; }
}
