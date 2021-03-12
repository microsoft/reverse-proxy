// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Utilities
{
    /// <summary>
    /// Provides a thread static implementation of random numbers that optimizes not to lock on every invocation of random number generation.
    /// </summary>
    internal class ThreadStaticRandom
    {
        [ThreadStatic]
        private static Random t_inst;
        public static Random Instance => t_inst ??= new Random();
    }
}
