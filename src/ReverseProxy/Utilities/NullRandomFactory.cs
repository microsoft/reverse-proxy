// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Yarp.ReverseProxy.Utilities;

internal class NullRandomFactory : IRandomFactory
{
    public Random CreateRandomInstance()
    {
        throw new NotImplementedException();
    }
}
