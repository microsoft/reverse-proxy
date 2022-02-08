// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Server.HttpSys;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Delegation;

public class HttpSysDelegationRuleManagerTests : TestAutoMockBase
{
    [Fact]
    public void OnClusterAdded_SingleDelegationDestination_RuleAdded()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);

        VerifyRuleAdded(sut, destination);
    }

    [Fact]
    public void OnClusterAdd_MultipleDelegationDestination_VerifyRulesAdded()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var destination2 = CreateDestination("dest2", "queue2");
        var cluster = CreateCluster("cluster1", destination1, destination2);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);

        VerifyRuleAdded(sut, destination1);
        VerifyRuleAdded(sut, destination2);
    }

    [Fact]
    public void OnClusterAdd_MultipleClustersWithSingleDelegationDestination_VerifyRulesAdded()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster1 = CreateCluster("cluster1", destination1);
        var destination2 = CreateDestination("dest2", "queue2");
        var cluster2 = CreateCluster("cluster2", destination2);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster1);
        sut.OnClusterAdded(cluster2);

        VerifyRuleAdded(sut, destination1);
        VerifyRuleAdded(sut, destination2);
    }

    [Fact]
    public void OnClusterAdd_NoDelegationDestinations_VerifyRuleNotAdded()
    {
        var destination = CreateDestination("dest1", queueName: null);
        var cluster = CreateCluster("cluster1", destination);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);

        VerifyRuleNotAdded(sut, destination);
    }

    [Fact]
    public void OnClusterAdd_CreateRuleThrows_VerifyRuleNotAdded()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);
        Mock<IServerDelegationFeature>()
            .Setup(x => x.CreateDelegationRule(It.IsAny<string>(), It.IsAny<string>()))
            .Throws<Exception>()
            .Verifiable();

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);

        VerifyRuleNotExists(sut, destination);
        VerifyCreateDelegationRuleCalled(destination);
    }

    [Fact]
    public void OnClusterChanged_RuleExists_NewRuleAdded_VerifyNewRuleAdded()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", "queue2");
        cluster = CreateCluster(cluster.ClusterId, destination1, destination2);

        sut.OnClusterChanged(cluster);

        VerifyRuleAdded(sut, destination2);
        VerifyRuleExists(sut, destination1);
        VerifyCreateDelegationRuleNotCalled(destination1);
    }

    [Fact]
    public void OnClusterChanged_RuleExists_NoNewRuleAdded_VerifyRuleNotAdded()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", queueName: null);
        cluster = CreateCluster(cluster.ClusterId, destination1, destination2);

        sut.OnClusterChanged(cluster);

        VerifyRuleExists(sut, destination1);
        VerifyCreateDelegationRuleNotCalled(destination1);
        VerifyRuleNotAdded(sut, destination2);
    }

    [Fact]
    public void OnClusterChanged_RuleExists_RuleRemoved_VerifyRuleRemoved()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", queueName: null);
        cluster = CreateCluster(cluster.ClusterId, destination2);

        sut.OnClusterChanged(cluster);

        VerifyRuleNotAdded(sut, destination1);
        VerifyRuleNotAdded(sut, destination2);
    }

    [Fact]
    public void OnClusterChanged_RuleExists_RuleRemovedAndNewRuleAdded_VerifyRuleRemoved()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination1);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        var destination2 = CreateDestination("dest2", "queue2");
        cluster = CreateCluster(cluster.ClusterId, destination2);

        sut.OnClusterChanged(cluster);

        VerifyRuleAdded(sut, destination2);
        VerifyRuleNotAdded(sut, destination1);
    }

    [Fact]
    public void OnClusterChanged_NoRuleExists_NewRuleAdded_VerifyNewRuleAdded()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterChanged(cluster);

        VerifyRuleAdded(sut, destination);
    }

    [Fact]
    public void OnClusterChanged_NoRuleExists_NoNewRuleAdded_VerifyRuleNotAdded()
    {
        var destination = CreateDestination("dest1", queueName: null);
        var cluster = CreateCluster("cluster1", destination);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterChanged(cluster);

        VerifyRuleNotAdded(sut, destination);
    }

    [Fact]
    public void OnClusterRemoved_RuleExists_VerifyRuleRemoved()
    {
        var destination = CreateDestination("dest1", "queue1");
        var cluster = CreateCluster("cluster1", destination);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster);
        Mock<IServerDelegationFeature>().Reset();

        sut.OnClusterRemoved(cluster);

        VerifyRuleNotAdded(sut, destination);
    }

    [Fact]
    public void OnClusterRemoved_NoRuleExists()
    {
        var cluster = CreateCluster("cluster1");

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterRemoved(cluster);
    }

    [Fact]
    public void Dipose_AllRulesRemoved()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var cluster1 = CreateCluster("cluster1", destination1);
        var destination2 = CreateDestination("dest2", "queue2");
        var cluster2 = CreateCluster("cluster2", destination2);

        var sut = Create<HttpSysDelegationRuleManager>();
        sut.OnClusterAdded(cluster1);
        sut.OnClusterAdded(cluster2);

        sut.Dispose();

        VerifyRuleNotExists(sut, destination1);
        VerifyRuleNotExists(sut, destination2);
    }

    private void VerifyRuleNotExists(HttpSysDelegationRuleManager sut, DestinationState destination)
    {
        var ruleFound = sut.TryGetDelegationRule(destination, out _);
        Assert.False(ruleFound);
    }

    private void VerifyRuleExists(HttpSysDelegationRuleManager sut, DestinationState destination)
    {
        var ruleFound = sut.TryGetDelegationRule(destination, out _);
        Assert.True(ruleFound);
    }

    private void VerifyCreateDelegationRuleCalled(DestinationState destination)
    {
        Mock<IServerDelegationFeature>()
            .Verify(m => m.CreateDelegationRule(destination.GetHttpSysQueueName(), destination.Model.Config.Address), Times.Once());
    }

    private void VerifyCreateDelegationRuleNotCalled(DestinationState destination)
    {
        Mock<IServerDelegationFeature>()
            .Verify(m => m.CreateDelegationRule(destination.GetHttpSysQueueName(), destination.Model.Config.Address), Times.Never());
    }

    private void VerifyRuleAdded(HttpSysDelegationRuleManager sut, DestinationState destination)
    {
        VerifyRuleExists(sut, destination);
        VerifyCreateDelegationRuleCalled(destination);
    }

    private void VerifyRuleNotAdded(HttpSysDelegationRuleManager sut, DestinationState destination)
    {
        VerifyRuleNotExists(sut, destination);
        VerifyCreateDelegationRuleNotCalled(destination);
    }

    private static ClusterState CreateCluster(string id, params DestinationState[] destinations)
    {
        return new ClusterState(id)
        {
            DestinationsState = new ClusterDestinationsState(allDestinations: destinations, availableDestinations: new DestinationState[0]),
        };
    }

    private static DestinationState CreateDestination(string id, string queueName = null)
    {
        var metadata = new Dictionary<string, string>();
        if (queueName != null)
        {
            metadata.Add(DelegationExtensions.HttpSysQueueNameMetadataKey, queueName);
        }

        var config = new DestinationConfig()
        {
            Address = "http://*:80",
            Metadata = metadata,
        };

        return new DestinationState(id)
        {
            Model = new DestinationModel(config),
        };
    }
}
#endif
