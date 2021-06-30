// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Forwarder
{
    internal sealed class ForwarderErrorFeature : IForwarderErrorFeature
    {
        internal ForwarderErrorFeature(ForwarderError error, Exception? ex)
        {
            Error = error;
            Exception = ex;
        }

        /// <summary>
        /// The specified ForwarderError.
        /// </summary>
        public ForwarderError Error { get; }

        /// <summary>
        /// The error, if any.
        /// </summary>
        public Exception? Exception { get; }
    }
}
