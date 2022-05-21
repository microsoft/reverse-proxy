// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.Kubernetes.Controller;

public class YarpOptions
{
    public string ControllerClass { get; set; }
    
    public bool ServerCertificates { get; set; }

    public string DefaultSslCertificate { get; set; }
}
