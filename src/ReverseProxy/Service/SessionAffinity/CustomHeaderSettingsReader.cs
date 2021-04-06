// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Abstractions.Config;

namespace Yarp.ReverseProxy.Service.SessionAffinity
{
    internal class CustomHeaderSettingsReader : IProxySettingsReader
    {
        private const string CustomHeaderNameKey = "CustomHeaderName";

        public KeyValuePair<Type, object> ReadSettings(Cluster cluster)
        {
            if (!(cluster.SessionAffinity?.Enabled ?? false))
            {
                return default;
            }

            if (cluster.Metadata != null && cluster.Metadata.TryGetValue(CustomHeaderNameKey, out var value))
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(); // TODO: Validation, error message.
                }

                return new KeyValuePair<Type, object>(typeof(CustomHeaderSessionAffinitySettings), new CustomHeaderSessionAffinitySettings
                {
                    CustomHeaderName = value,
                });
            }

            return default;
        }
    }
}
