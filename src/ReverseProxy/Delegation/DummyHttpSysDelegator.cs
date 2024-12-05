// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Yarp.ReverseProxy.Delegation;

// Only used as part of a workaround for https://github.com/dotnet/aspnetcore/issues/59166.
internal sealed class DummyHttpSysDelegator : IHttpSysDelegator
{
    public void ResetQueue(string queueName, string urlPrefix) { }
}
