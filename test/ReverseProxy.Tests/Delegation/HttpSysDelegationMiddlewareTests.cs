#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Moq;
using Xunit;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Utilities;
using Yarp.Tests.Common;

namespace Yarp.ReverseProxy.Delegation;

public class HttpSysDelegationMiddlewareTests : TestAutoMockBase
{
    private readonly HttpSysDelegationMiddleware _sut;
    private readonly RequestDelegate _next;
    private readonly DefaultHttpContext _context;
    private readonly ReverseProxyFeature _proxyFeature;
    private readonly List<DestinationState> _availableDestinations;

    private Action _nextCallback;
    private bool _nextCalled;

    public HttpSysDelegationMiddlewareTests()
    {
        _context = new DefaultHttpContext();
        _availableDestinations = new List<DestinationState>();
        _proxyFeature = new ReverseProxyFeature
        {
            AvailableDestinations = _availableDestinations,
            Cluster = new ClusterModel(new ClusterConfig(), new HttpMessageInvoker(Mock<HttpMessageHandler>().Object)),
        };
        _context.Features.Set<IReverseProxyFeature>(_proxyFeature);
        _context.Features.Set(Mock<IHttpSysRequestDelegationFeature>().Object);

        _next = context =>
        {
            _nextCalled = true;
            _nextCallback?.Invoke();
            return Task.CompletedTask;
        };
        Provide(_next);

        _sut = Create<HttpSysDelegationMiddleware>();
    }

    [Fact]
    public async Task SingleDelegationDestination_VerifyRequestDelegatedAndNextNotCalled()
    {
        var destination = CreateDestination("dest1", "queue1");
        _availableDestinations.Add(destination);
        SetupCanDelegate(true);
        SetupTryGetDelegationRule(ruleExists: true);

        await _sut.Invoke(_context);

        VerifyRequestDelegated(destination);
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task NoDestinations_VerifyNextInvoked()
    {
        await _sut.Invoke(_context);

        Assert.True(_nextCalled);
        Mock<IHttpSysDelegationRuleManager>().VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NoDelegationDestinations_VerifyNextInvoked()
    {
        _availableDestinations.Add(CreateDestination("dest1", queueName: null));

        await _sut.Invoke(_context);

        Assert.True(_nextCalled);
        Mock<IHttpSysDelegationRuleManager>().VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MultipleDestinations_OneDelegationAndOneProxyDestination_ProxyChoosen_VerifyNextInvokedWithSingleProxyDestination()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var destination2 = CreateDestination("dest2", queueName: null);
        _availableDestinations.Add(destination1);
        _availableDestinations.Add(destination2);
        SetupRandomToReturn(1); // return "dest2"

        _nextCallback = () =>
        {
            Assert.Equal(1, _proxyFeature.AvailableDestinations.Count);
            Assert.Same(destination2, _proxyFeature.AvailableDestinations[0]);
        };

        await _sut.Invoke(_context);

        Assert.True(_nextCalled);
        Mock<IHttpSysDelegationRuleManager>().VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MultipleDestinations_OneDelegationAndOneProxyDestination_DelegationChoosen_VerifyRequestDelegatedAndNextNotCalled()
    {
        var destination1 = CreateDestination("dest1", "queue1");
        var destination2 = CreateDestination("dest2", queueName: null);
        _availableDestinations.Add(destination1);
        _availableDestinations.Add(destination2);
        SetupRandomToReturn(0); // return "dest1"
        SetupCanDelegate(true);
        SetupTryGetDelegationRule(ruleExists: true);

        await _sut.Invoke(_context);

        VerifyRequestDelegated(destination1);
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task DelegationDestination_NoDelegationFeature_VerifyThrows()
    {
        _availableDestinations.Add(CreateDestination("dest1", "queue1"));
        _context.Features.Set<IHttpSysRequestDelegationFeature>(null);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => _sut.Invoke(_context));
    }

    [Fact]
    public async Task DelegationDestination_CanNotDelegate_VerifyThrowsAndErrorFeatureSet()
    {
        _availableDestinations.Add(CreateDestination("dest1", "queue1"));
        SetupCanDelegate(false);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => _sut.Invoke(_context));
    }

    [Fact]
    public async Task DelegationDestination_DelegationRuleNotFound_Verify503SatusAndErrorFeatureSet()
    {
        _availableDestinations.Add(CreateDestination("dest1", "queue1"));
        SetupCanDelegate(true);
        SetupTryGetDelegationRule(ruleExists: false);

        await _sut.Invoke(_context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, _context.Response.StatusCode);
        var errorFeature = _context.Features.Get<IForwarderErrorFeature>();
        Assert.NotNull(errorFeature);
        Assert.Equal(ForwarderError.HttpSysDelegationRuleNotFound, errorFeature.Error);
        Assert.Null(errorFeature.Exception);
    }

    [Fact]
    public async Task DelegationDestination_DelegationFailed_Verify503SatusAndErrorFeatureSet()
    {
        _availableDestinations.Add(CreateDestination("dest1", "queue1"));
        SetupCanDelegate(true);
        SetupTryGetDelegationRule(ruleExists: true);
        Mock<IHttpSysRequestDelegationFeature>()
            .Setup(m => m.DelegateRequest(It.IsAny<DelegationRule>()))
            .Throws<Exception>();

        await _sut.Invoke(_context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, _context.Response.StatusCode);
        var errorFeature = _context.Features.Get<IForwarderErrorFeature>();
        Assert.NotNull(errorFeature);
        Assert.Equal(ForwarderError.HttpSysDelegationFailed, errorFeature.Error);
        Assert.NotNull(errorFeature.Exception);
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

    private void SetupRandomToReturn(int value)
    {
        Mock<IRandomFactory>().Setup(m => m.CreateRandomInstance()).Returns(new TestRandom(value));
    }

    private void SetupCanDelegate(bool canDelegate)
    {
        Mock<IHttpSysRequestDelegationFeature>()
            .SetupGet(p => p.CanDelegate)
            .Returns(canDelegate);
    }

    private void SetupTryGetDelegationRule(bool ruleExists)
    {
        Mock<IHttpSysDelegationRuleManager>()
            .Setup(m => m.TryGetDelegationRule(It.IsAny<DestinationState>(), out It.Ref<DelegationRule>.IsAny))
            .Returns(ruleExists);
    }

    private void VerifyTryGetDelegationRuleCalled(DestinationState destination)
    {
        DelegationRule delegationRule;
        Mock<IHttpSysDelegationRuleManager>()
            .Verify(m => m.TryGetDelegationRule(destination, out delegationRule), Times.Once());
    }

    private void VerifyRequestDelegated(DestinationState destination)
    {
        Assert.Same(destination, _proxyFeature.ProxiedDestination);
        VerifyTryGetDelegationRuleCalled(destination);
        Mock<IHttpSysRequestDelegationFeature>()
            .Verify(m => m.DelegateRequest(null), Times.Once());
    }

    private class TestRandom : Random
    {
        private readonly int _next;

        public TestRandom(int next)
        {
            _next = next;
        }

        public override int Next(int maxValue)
        {
            return _next;
        }
    }
}
#endif
