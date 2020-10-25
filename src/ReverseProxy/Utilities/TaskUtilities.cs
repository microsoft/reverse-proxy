// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Utilities
{
    internal static class TaskUtilities
    {
        internal static readonly Task<bool> TrueTask = Task.FromResult(true);
        internal static readonly Task<bool> FalseTask = Task.FromResult(false);
    }
}
