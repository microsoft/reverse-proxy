// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Kubernetes.Testing.Models;

public class ListResult
{
    public string Continue { get; set; }

    public string ResourceVersion { get; set; }

#pragma warning disable CA2227 // Collection properties should be read only
    public IList<ResourceObject> Items { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
}
