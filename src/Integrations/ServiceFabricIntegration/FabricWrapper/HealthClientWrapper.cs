// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Fabric;
using System.Fabric.Health;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <inheritdoc/>
    internal class HealthClientWrapper : IHealthClientWrapper
    {
        private readonly FabricClient.HealthClient healthClient;

        public HealthClientWrapper()
        {
            this.healthClient = new FabricClient().HealthManager;
        }

        /// <inheritdoc/>
        public void ReportHealth(HealthReport healthReport, HealthReportSendOptions sendOptions)
        {
            this.healthClient.ReportHealth(healthReport, sendOptions);
        }
    }
}
