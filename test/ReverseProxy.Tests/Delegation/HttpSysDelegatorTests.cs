// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Delegation;

public class HttpSysDelegatorTests : TestAutoMockBase
{
    private readonly HttpSysDelegator _delegator;
    private readonly IClusterChangeListener _changeListener;

    private readonly DefaultHttpContext _context;


    public HttpSysDelegatorTests()
    {
        _delegator = Create<HttpSysDelegator>();
        _changeListener = _delegator;

        _context = new DefaultHttpContext();
        _context.Features.Set(Mock<IHttpSysRequestDelegationFeature>().Object);

        SetupCanDelegate(true);
    }

    [Fact]
    public void DelegateRequest_NoDelegationFeature_VerifyThrows()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        _changeListener.OnClusterAdded(cluster);
        _context.Features.Set<IHttpSysRequestDelegationFeature>(null);

        Assert.ThrowsAny<InvalidOperationException>(() => DelegateRequest(destination));
    }

    [Fact]
    public void DelegateRequest_CanNotDelegate_VerifyThrowsAndErrorFeatureSet()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        _changeListener.OnClusterAdded(cluster);
        SetupCanDelegate(false);

        Assert.ThrowsAny<InvalidOperationException>(() => DelegateRequest(destination));
    }

    [Fact]
    public void DelegateRequest_DelegationRuleNotFound_Verify503SatusAndErrorFeatureSet()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        DelegateRequest(CreateDestination("dest1", "invalidQueue"));

        VerifyNoAvailableDestinationsError();
    }

    [Fact]
    public void DelegateRequest_CreateRuleFailed_Verify503SatusAndErrorFeatureSet()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);
        Mock<IServerDelegationFeature>()
            .Setup(x => x.CreateDelegationRule(It.IsAny<string>(), It.IsAny<string>()))
            .Throws<Exception>()
            .Verifiable();

        _changeListener.OnClusterAdded(cluster);

        DelegateRequest(destination);

        VerifyNoAvailableDestinationsError();
    }

    [Fact]
    public void OnClusterAdded_SingleDelegationDestination_RuleCreated()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleCreated(destination);
    }

    [Fact]
    public void OnClusterAdd_MultipleDelegationDestination_VerifyRulesCreated()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var destination2 = CreateDestination("dest2", "queue2");
        var cluster = CreateCluster("cluster1", destination1, destination2);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleCreated(destination1);
        VerifyDelegationRuleCreated(destination2);
    }

    [Fact]
    public void OnClusterAdd_MultipleDelegationDestinationWithSameQueue_VerifyRuleCreated()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var destination2 = CreateDestination("dest2", "queue1");
        var cluster = CreateCluster("cluster1", destination1, destination2);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleCreated(destination1);
    }

    [Fact]
    public void OnClusterAdd_MultipleClustersEachWithSingleDelegationDestination_VerifyRulesCreated()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster1 = CreateCluster("cluster1", destination1);
        var destination2 = CreateDestination("dest2", "queue2");
        var cluster2 = CreateCluster("cluster2", destination2);

        _changeListener.OnClusterAdded(cluster1);
        _changeListener.OnClusterAdded(cluster2);

        VerifyDelegationRuleCreated(destination1);
        VerifyDelegationRuleCreated(destination2);
    }

    [Fact]
    public void OnClusterAdd_NoDelegationDestinations_VerifyRuleNotCreated()
    {
        var destination = CreateDestination("dest1", queueName: null);
        var cluster = CreateCluster("cluster1", destination);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleNotCreated(destination);
    }

    [Fact]
    public void OnClusterChanged_RuleExists_NewRuleAdded_VerifyNewRuleCreated()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        _changeListener.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", "queue2");
        cluster = CreateCluster(cluster.ClusterId, destination1, destination2);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleCreated(destination2);
        VerifyDelegationRuleNotCreated(destination1);
    }

    [Fact]
    public void OnClusterChanged_RuleExists_NoNewRuleAdded_VerifyRuleNotCreated()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        _changeListener.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", queueName: null);
        cluster = CreateCluster(cluster.ClusterId, destination1, destination2);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleNotCreated(destination1);
        VerifyDelegationRuleNotCreated(destination2);
    }

    [Fact]
    public void OnClusterChanged_RuleExists_RuleRemoved_VerifyRuleRemoved()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        _changeListener.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", queueName: null);
        cluster = CreateCluster(cluster.ClusterId, destination2);

        _changeListener.OnClusterAdded(cluster);

        DelegateRequest(destination1);

        VerifyDelegationRuleNotCreated(destination2);
        VerifyNoAvailableDestinationsError();
    }

    [Fact]
    public void OnClusterChanged_RuleExists_RuleRemovedAndNewRuleAdded_VerifyRuleRemoved()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        _changeListener.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", "queue2");
        cluster = CreateCluster(cluster.ClusterId, destination2);

        _changeListener.OnClusterAdded(cluster);

        DelegateRequest(destination1);

        VerifyNoAvailableDestinationsError();
        VerifyDelegationRuleCreated(destination2);
    }

    [Fact]
    public void OnClusterChanged_NoRuleExists_NewRuleAdded_VerifyNewRuleCreated()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleCreated(destination);
    }

    [Fact]
    public void OnClusterChanged_NoRuleExists_NoNewRuleAdded_VerifyRuleNotCreated()
    {
        var destination = CreateDestination("dest1", queueName: null);
        var cluster = CreateCluster("cluster1", destination);

        _changeListener.OnClusterAdded(cluster);

        VerifyDelegationRuleNotCreated(destination);
    }

    [Fact]
    public void OnClusterRemoved_RuleExists_VerifyRuleRemoved()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        _changeListener.OnClusterAdded(cluster);
        _changeListener.OnClusterRemoved(cluster);

        DelegateRequest(destination);

        VerifyNoAvailableDestinationsError();
    }

    [Fact]
    public void OnClusterRemoved_NoRuleExists()
    {
        var cluster = CreateCluster("cluster1");

        _changeListener.OnClusterRemoved(cluster);
    }

    private void SetupCanDelegate(bool canDelegate)
    {
        Mock<IHttpSysRequestDelegationFeature>()
            .SetupGet(p => p.CanDelegate)
            .Returns(canDelegate);
    }

    private void DelegateRequest(DestinationState destination)
    {
        _delegator.DelegateRequest(_context, destination.GetHttpSysDelegationQueue(), destination.Model.Config.Address);
    }

    private void VerifyNoAvailableDestinationsError()
    {
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, _context.Response.StatusCode);
        var errorFeature = _context.Features.Get<IForwarderErrorFeature>();
        Assert.NotNull(errorFeature);
        Assert.Equal(ForwarderError.NoAvailableDestinations, errorFeature.Error);
    }

    private void VerifyRequestError()
    {
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, _context.Response.StatusCode);
        var errorFeature = _context.Features.Get<IForwarderErrorFeature>();
        Assert.NotNull(errorFeature);
        Assert.Equal(ForwarderError.Request, errorFeature.Error);
        Assert.NotNull(errorFeature.Exception);
    }

    private void VerifyDelegationRuleCreated(DestinationState destination)
    {
        Mock<IServerDelegationFeature>()
            .Verify(m => m.CreateDelegationRule(destination.GetHttpSysDelegationQueue(), destination.Model.Config.Address), Times.Once());
    }

    private void VerifyDelegationRuleNotCreated(DestinationState destination)
    {
        Mock<IServerDelegationFeature>()
            .Verify(m => m.CreateDelegationRule(destination.GetHttpSysDelegationQueue(), destination.Model.Config.Address), Times.Never());
    }

    private static ClusterState CreateCluster(string id, params DestinationState[] destinations)
    {
        var cluster = new ClusterState(id);
        foreach (var destination in destinations)
        {
            cluster.Destinations.TryAdd(destination.DestinationId, destination);
        }

        return cluster;
    }

    private static DestinationState CreateDestination(string id, string queueName = null)
    {
        var metadata = new Dictionary<string, string>();
        if (queueName != null)
        {
            metadata.Add(DelegationExtensions.HttpSysDelegationQueueMetadataKey, queueName);
        }

        var config = new DestinationConfig()
        {
            Address = "http://*:80/",
            Metadata = metadata,
        };

        return new DestinationState(id)
        {
            Model = new DestinationModel(config),
        };
    }
}
#endif
