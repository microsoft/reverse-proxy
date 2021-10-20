// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System.Collections.Generic;

namespace Microsoft.Kubernetes.Operator.Generators
{
    /// <summary>
    /// Class GenerateResult is returned from "IOperatorGenerator.GenerateAsync"/>
    /// to determine how the operator should proceed.
    /// </summary>
    public class GenerateResult
    {
        public GenerateResult()
        {
            Resources = new List<IKubernetesObject<V1ObjectMeta>>();
        }

        public GenerateResult(IList<IKubernetesObject<V1ObjectMeta>> resources)
        {
            Resources = resources ?? throw new System.ArgumentNullException(nameof(resources));
        }

        public GenerateResult(bool shouldReconcile, IList<IKubernetesObject<V1ObjectMeta>> resources)
        {
            ShouldReconcile = shouldReconcile;
            Resources = resources ?? throw new System.ArgumentNullException(nameof(resources));
        }

        /// <summary>
        /// Gets or sets a value indicating whether the operator should reconcile.
        /// </summary>
        /// <value><c>true</c> if <see cref="Resources"/> should be applied to cluster; otherwise, <c>false</c>.</value>
        public bool ShouldReconcile { get; set; }

        /// <summary>
        /// Gets or sets the list of generated resources to reconcile.
        /// </summary>
        /// <value>The resources.</value>
        public IList<IKubernetesObject<V1ObjectMeta>> Resources { get; }
    }
}
