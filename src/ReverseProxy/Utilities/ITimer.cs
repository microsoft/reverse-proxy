// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Utilities
{
    internal interface ITimer : IDisposable
    {
        void Change(long dueTime, long period);
    }
}
