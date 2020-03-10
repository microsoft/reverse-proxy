// <copyright file="TestConfigError.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Tests.Common
{
    /// <summary>
    /// Describes a configuration error.
    /// </summary>
    public class TestConfigError
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Identifier of the element in the configuration that this error applies to.
        /// Can represent a backend, endpoint or route.
        /// </summary>
        public string ElementId { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Config error. ErrorCode='{this.ErrorCode}', ElementId='{this.ElementId}', Message='{this.Message}'.";
        }
    }
}
