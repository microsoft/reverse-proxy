// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Enables the implementor to inspect each route and conditionally add transforms.
    /// </summary>
    public interface ITransformProvider
    {
        /// <summary>
        /// Validates any route data needed for transforms.
        /// </summary>
        /// <param name="context">The context to add any generated errors to.</param>
        void Validate(TransformValidationContext context);

        /// <summary>
        /// Inspect the given route and conditionally add transforms.
        /// This is called for every route, each time that route is built.
        /// </summary>
        void Apply(TransformBuilderContext context);
    }
}
