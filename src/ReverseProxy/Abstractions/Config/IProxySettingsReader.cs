// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    public interface IProxySettingsReader
    {
        KeyValuePair<Type, object> ReadSettings(Cluster cluster);
    }
}
