// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NET6_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Delegation;

internal sealed class HttpSysDelegationRuleManager : IHttpSysDelegationRuleManager, IClusterChangeListener, IDisposable
{
    private readonly IServerDelegationFeature _delegationFeature;
    private readonly ILogger<HttpSysDelegationRuleManager> _logger;
    private readonly ConcurrentDictionary<RuleKey, DelegationRule> _rules;
    private readonly ConcurrentDictionary<string, HashSet<RuleKey>> _rulesPerCluster;

    private bool _disposed;

    public HttpSysDelegationRuleManager(
            IServerDelegationFeature delegationFeature,
            ILogger<HttpSysDelegationRuleManager> logger)
    {
        _delegationFeature = delegationFeature ?? throw new ArgumentNullException(nameof(delegationFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _rules = new ConcurrentDictionary<RuleKey, DelegationRule>();
        _rulesPerCluster = new ConcurrentDictionary<string, HashSet<RuleKey>>(StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetDelegationRule(DestinationState destination, [MaybeNullWhen(false)] out DelegationRule delegationRule)
    {
        _ = destination ?? throw new ArgumentNullException(nameof(destination));

        var queueName = destination.GetHttpSysQueueName();

        delegationRule = null;
        return queueName != null && _rules.TryGetValue(new RuleKey(destination.DestinationId, queueName), out delegationRule);
    }

    public void OnClusterAdded(ClusterState cluster)
    {
        if (!_disposed)
        {
            AddOrUpdateRules(cluster);
        }
    }

    public void OnClusterChanged(ClusterState cluster)
    {
        if (!_disposed)
        {
            AddOrUpdateRules(cluster);
        }
    }

    public void OnClusterRemoved(ClusterState cluster)
    {
        if (!_disposed)
        {
            RemoveRules(cluster.ClusterId);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            foreach (var entry in _rulesPerCluster)
            {
                RemoveRules(entry.Key);
            }
        }
    }

    private void AddOrUpdateRules(ClusterState cluster)
    {
        var incomingDestinations = cluster.DestinationsState.AllDestinations.Where(dest => dest.ShouldUseHttpSysQueueDelegation()).ToList();

        if (!incomingDestinations.Any() && !_rulesPerCluster.ContainsKey(cluster.ClusterId))
        {
            // No new or existing rules
            return;
        }

        var clusterId = cluster.ClusterId;
        var desiredRules = new HashSet<RuleKey>();
        var currentRules = _rulesPerCluster.GetOrAdd(
            clusterId,
            (key, size) => new HashSet<RuleKey>(size),
            incomingDestinations.Count);

        lock (currentRules)
        {
            foreach (var incomingDestination in incomingDestinations)
            {
                var queueName = incomingDestination.GetHttpSysQueueName();
                Debug.Assert(queueName != null);
                var ruleKey = new RuleKey(incomingDestination.DestinationId, queueName);

                var added = currentRules.Contains(ruleKey);
                if (!added)
                {
                    try
                    {
                        var rule = _delegationFeature.CreateDelegationRule(queueName, incomingDestination.Model.Config.Address);
                        added = _rules.TryAdd(ruleKey, rule);
                        if (!added)
                        {
                            // This should never happen because CreateDelegationRule will throw if a rule for the queue already exists
                            Debug.Fail($"Rule for {queueName} created but a rule for the queue was already added to dictionary");
                            rule?.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.CreateDelegationRuleFailed(
                            _logger,
                            cluster.ClusterId,
                            incomingDestination.DestinationId,
                            queueName,
                            incomingDestination.Model.Config.Address,
                            ex);
                    }
                }

                if (added)
                {
                    desiredRules.Add(ruleKey);
                }
            }

            currentRules.RemoveWhere(key =>
            {
                if (!desiredRules.Contains(key))
                {
                    RemoveRule(key);
                    return true;
                }

                return false;
            });

            currentRules.UnionWith(desiredRules);
        }
    }

    private void RemoveRules(string clusterId)
    {
        if (_rulesPerCluster.TryRemove(clusterId, out var currentRules))
        {
            lock (currentRules)
            {
                foreach (var ruleKey in currentRules)
                {
                    RemoveRule(ruleKey);
                }
            }
        }
    }

    private void RemoveRule(RuleKey ruleKey)
    {
        if (_rules.TryRemove(ruleKey, out var rule))
        {
            rule?.Dispose();
        }
    }

    private readonly struct RuleKey : IEquatable<RuleKey>
    {
        private readonly int _hashCode;

        public RuleKey(string destinationId, string queueName)
        {
            DestinationId = destinationId;
            QueueName = queueName;
            _hashCode = HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(destinationId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(queueName));
        }

        public string DestinationId { get; }

        public string QueueName { get; }

        public bool Equals(RuleKey other)
        {
            return _hashCode == other._hashCode
                && string.Equals(DestinationId, other.DestinationId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(QueueName, other.QueueName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is RuleKey other && Equals(other);
        }

        public override int GetHashCode() => _hashCode;
    }


    private static class Log
    {
        private static readonly Action<ILogger, string, string, string, string, Exception?> _createDelegationRuleFailed = LoggerMessage.Define<string, string, string, string>(
            LogLevel.Warning,
            EventIds.MultipleDestinationsAvailable,
            "Failed to create rule for destination '{destinationId}' in cluster '{clusterId}' with queue name '{queueName}' and url prefix '{urlPrefix}'.");

        public static void CreateDelegationRuleFailed(ILogger logger, string clusterId, string destinationId, string queueName, string urlPrefix, Exception ex)
        {
            _createDelegationRuleFailed(logger, clusterId, destinationId, queueName, urlPrefix, ex);
        }
    }
}
#endif
