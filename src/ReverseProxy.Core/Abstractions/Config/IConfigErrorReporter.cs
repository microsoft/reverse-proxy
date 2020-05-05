// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

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

        /// <summary>
        /// Reports a configuration error.
        /// </summary>
        void ReportError(string code, string itemId, string message, Exception ex);
    }
}
