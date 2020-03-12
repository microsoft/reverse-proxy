// <copyright file="IConfigErrorReporter.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.Abstractions
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