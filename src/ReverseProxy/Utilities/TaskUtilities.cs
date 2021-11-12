// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Utilities;

internal static class TaskUtilities
{
    internal static readonly Task<bool> TrueTask = Task.FromResult(true);
    internal static readonly Task<bool> FalseTask = Task.FromResult(false);
}
