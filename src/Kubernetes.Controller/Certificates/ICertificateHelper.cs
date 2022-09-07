// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;
using k8s.Models;

namespace Yarp.Kubernetes.Controller.Certificates;

public interface ICertificateHelper
{
    X509Certificate2 ConvertCertificate(NamespacedName namespacedName, V1Secret secret);
}
