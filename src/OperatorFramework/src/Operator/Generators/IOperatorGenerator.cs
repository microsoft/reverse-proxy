// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Operator.Generators;

public interface IOperatorGenerator<TResource>
{
    /// <summary>
    /// Generates the specified resource. The resource document as the input is the source
    /// of truth, and the same set of child resources should be generated in response every
    /// time this method is called. The generated resources are then reconciled against the
    /// current state of the cluster, and any differences result in Create/Patch/Delete operations
    /// to bring the cluster in line with the desired state.
    /// </summary>
    /// <param name="resource">The resource which this particular operator takes as input.</param>
    /// <returns>GenerateResult that determines what should be reconciled.</returns>
    Task<GenerateResult> GenerateAsync(TResource resource);
}
