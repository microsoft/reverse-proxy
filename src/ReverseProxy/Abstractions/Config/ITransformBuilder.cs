// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Service
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
        IList<Exception> Validate(IList<IDictionary<string, string>> transforms);

        /// <summary>
        /// Builds the given transforms into executable rules.
        /// </summary>
        HttpTransformer Build(IList<IDictionary<string, string>> rawTransforms);
    }
}
