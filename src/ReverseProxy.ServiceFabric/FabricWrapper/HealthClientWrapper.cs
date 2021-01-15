// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Fabric;
using System.Fabric.Health;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <inheritdoc/>
    internal class HealthClientWrapper : IHealthClientWrapper
    {
        private readonly FabricClient.HealthClient _healthClient;

        public HealthClientWrapper(IFabricClientWrapper fabricClientWrapper)
        {
            _healthClient = fabricClientWrapper.FabricClient.HealthManager;
        }

        /// <inheritdoc/>
        public void ReportHealth(HealthReport healthReport, HealthReportSendOptions sendOptions)
        {
            _healthClient.ReportHealth(healthReport, sendOptions);
        }
    }
}
