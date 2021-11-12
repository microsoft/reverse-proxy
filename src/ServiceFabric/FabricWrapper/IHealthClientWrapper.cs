// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Fabric.Health;

namespace Yarp.ReverseProxy.ServiceFabric;

/// <summary>
/// A wrapper for the service fabric health client SDK to make service fabric API unit testable.
/// Microsoft documentation: https://docs.microsoft.com/en-us/dotnet/api/system.fabric.fabricclient.healthclient?view=azure-dotnet.
/// </summary>
internal interface IHealthClientWrapper
{
    /// <summary>
    /// Reports health on a Service Fabric entity.
    /// </summary>
    void ReportHealth(HealthReport healthReport, HealthReportSendOptions sendOptions);
}
