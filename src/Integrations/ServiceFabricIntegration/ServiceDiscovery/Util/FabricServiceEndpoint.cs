// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration
{
    /// <summary>
    /// Identifies a Fabric service and endpoint name.
    /// </summary>
    // TODO: we might want to have a public interface for this to let users define custom behavior
    internal sealed class FabricServiceEndpoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FabricServiceEndpoint"/> class.
        /// </summary>
        /// <remarks>
        /// To prevent an explosion of overload combination, this overload permits nulls in any parameter, selecting the default value
        /// for those cases.
        /// </remarks>
        /// <param name="listenerNames">Sets <see cref="ListenerNames"/>If null, defaults to a single endpoint named "".</param>
        /// <param name="allowedSchemePredicate">Sets <see cref="AllowedSchemePredicate"/>If null, defaults to allow all schemes. In the future, the default is likely to change to allow only https.</param>
        /// <param name="emptyStringMatchesAnyListener">Sets <see cref="EmptyStringMatchesAnyListener"/>. If null, defaults to false.</param>
        public FabricServiceEndpoint(
            IEnumerable<string> listenerNames = null,
            Func<string, bool> allowedSchemePredicate = null,
            bool? emptyStringMatchesAnyListener = null)
        {
            Contracts.Check(listenerNames == null || listenerNames.Count() > 0, nameof(listenerNames));

            this.ListenerNames = listenerNames ?? new[] { string.Empty };

            this.AllowedSchemePredicate = allowedSchemePredicate ?? (_ => true);

            this.EmptyStringMatchesAnyListener = emptyStringMatchesAnyListener ?? false;
        }

        /// <summary>
        /// Gets the name of the first endpoint that will used, if available on the partition.
        /// </summary>
        public string ListenerName => this.ListenerNames.First();

        /// <summary>
        /// Gets the list of named endpoints that will be tried, in order of preference.
        /// </summary>
        public IEnumerable<string> ListenerNames { get; }

        /// <summary>
        /// If set, will only considers listeners where this predicate returns true for the uri scheme (e.g. "https" scheme).
        /// </summary>
        public Func<string, bool> AllowedSchemePredicate { get; }

        /// <summary>
        /// Will match first listener with appropriate scheme. Attempts to mimic behavior of service fabric reverse proxy when used
        /// with no listener.
        /// </summary>
        public bool EmptyStringMatchesAnyListener { get; }
    }
}
