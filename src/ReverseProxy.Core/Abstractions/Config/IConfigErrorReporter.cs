// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Provides a method that is called when a configuration error is reported.
    /// </summary>
    public interface IConfigErrorReporter
    {
        /// <summary>
        /// Reports a configuration error.
        /// </summary>
        void ReportError(string code, string itemId, string message);
    }
}
