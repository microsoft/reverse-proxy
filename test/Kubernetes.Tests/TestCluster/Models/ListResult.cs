// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Yarp.Kubernetes.Tests.TestCluster.Models;

public class ListResult
{
    public string Continue { get; set; }

    public string ResourceVersion { get; set; }

    public IList<ResourceObject> Items { get; set; }
}
