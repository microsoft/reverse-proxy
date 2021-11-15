// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Resources.Models;

public class StandardTestYaml
{
    public ResourceKind ResourceKind { get; set; }
    public JToken Apply { get; set; }
    public JToken Live { get; set; }
    public JToken LastApplied { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
    public List<PatchOperation> Patch { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
}

