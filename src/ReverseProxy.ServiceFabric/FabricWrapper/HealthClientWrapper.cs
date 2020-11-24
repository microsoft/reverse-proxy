// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Fabric;
using System.Fabric.Health;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <inheritdoc/>
    internal class HealthClientWrapper : IHealthClientWrapper , IDisposable
    {
        private readonly FabricClient _fabricClient;
        private readonly FabricClient.HealthClient _healthClient;

        public HealthClientWrapper()
        {
            _fabricClient = new FabricClient();
            _healthClient = _fabricClient.HealthManager;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _fabricClient.Dispose();
        }

        /// <inheritdoc/>
        public void ReportHealth(HealthReport healthReport, HealthReportSendOptions sendOptions)
        {
            _healthClient.ReportHealth(healthReport, sendOptions);
        }
    }
}
