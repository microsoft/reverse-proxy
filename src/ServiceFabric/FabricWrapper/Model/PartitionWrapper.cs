// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.ServiceFabric;

internal sealed class PartitionWrapper
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}
