// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
{
    /// <summary>
    /// Validates and builds request and response transforms for a given route.
    /// </summary>
    public interface ITransformBuilder
    {
        /// <summary>
        /// Validates that each transform is known and has the expected parameters. All transforms are validated and
        /// so all errors can be reported.
        /// </summary>
        /// <returns>True if all transforms are valid, otherwise false.</returns>
        bool Validate(IList<IDictionary<string, string>> transforms, string routeId);

        /// <summary>
        /// Builds the given transforms into executable rules.
        /// </summary>
        Transforms Build(IList<IDictionary<string, string>> rawTransforms);
    }
}
