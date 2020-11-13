// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy.ServiceFabric
{
    /// <inheritdoc/>
    internal class ServiceExtensionLabelsProvider : IServiceExtensionLabelsProvider
    {
        internal static readonly XNamespace XNSServiceManifest = "http://schemas.microsoft.com/2011/01/fabric";
        internal static readonly XNamespace XNSFabricNoSchema = "http://schemas.microsoft.com/2015/03/fabact-no-schema";
        internal static readonly XName XNameLabel = XNSFabricNoSchema + "Label";
        internal static readonly XName XNameLabels = XNSFabricNoSchema + "Labels";

        private const string ExtensionName = "YARP-preview";

        private readonly ILogger<ServiceExtensionLabelsProvider> _logger;
        private readonly IServiceFabricCaller _serviceFabricCaller;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceExtensionLabelsProvider"/> class.
        /// </summary>
        public ServiceExtensionLabelsProvider(
            ILogger<ServiceExtensionLabelsProvider> logger,
            IServiceFabricCaller serviceFabricCaller)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceFabricCaller = serviceFabricCaller ?? throw new ArgumentNullException(nameof(serviceFabricCaller));
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> GetExtensionLabelsAsync(ApplicationWrapper application, ServiceWrapper service, CancellationToken cancellationToken)
        {
            _ = application ?? throw new ArgumentNullException(nameof(application));
            _ = service ?? throw new ArgumentNullException(nameof(service));
            _ = application.ApplicationTypeName ?? throw new ArgumentNullException($"{nameof(application)}.{nameof(application.ApplicationTypeName)}");
            _ = application.ApplicationTypeVersion ?? throw new ArgumentNullException($"{nameof(application)}.{nameof(application.ApplicationTypeVersion)}");
            _ = service.ServiceTypeName ?? throw new ArgumentNullException($"{nameof(service)}.{nameof(service.ServiceTypeName)}");
            _ = service.ServiceName ?? throw new ArgumentNullException($"{nameof(service)}.{nameof(service.ServiceName)}");

            string serviceManifestName;
            try
            {
                serviceManifestName = await _serviceFabricCaller.GetServiceManifestName(application.ApplicationTypeName, application.ApplicationTypeVersion, service.ServiceTypeName, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) // TODO: davidni: not fatal?
            {
                throw new ServiceFabricIntegrationException($"Failed to get service manifest name for service type {service.ServiceTypeName} of application type {application.ApplicationTypeName} {application.ApplicationTypeVersion} from Service Fabric: {ex}.");
            }

            if (serviceManifestName == null)
            {
                throw new ServiceFabricIntegrationException($"No service manifest name was found for service type {service.ServiceTypeName} of application type {application.ApplicationTypeName} {application.ApplicationTypeVersion}.");
            }

            string rawServiceManifest;
            try
            {
                rawServiceManifest = await _serviceFabricCaller.GetServiceManifestAsync(application.ApplicationTypeName, application.ApplicationTypeVersion, serviceManifestName, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) // TODO: davidni: not fatal?
            {
                throw new ServiceFabricIntegrationException($"Failed to get service manifest {serviceManifestName} of service type {service.ServiceTypeName} of application type {application.ApplicationTypeName} {application.ApplicationTypeVersion} from Service Fabric: {ex}.");
            }

            if (rawServiceManifest == null)
            {
                throw new ServiceFabricIntegrationException($"No service manifest named '{serviceManifestName}' was found for service type {service.ServiceTypeName} of application type {application.ApplicationTypeName} {application.ApplicationTypeVersion}.");
            }

            // TODO: gathering labels from multiple servicetypes within the same service would result in multiple
            // calls to the SF client and multiple XML parses. We should consider creating an instance of this class
            // per application type to reuse that data. Since this is uncommon, for now we follow the na�ve implementation.
            var result = await ExtractLabelsAsync(rawServiceManifest, service.ServiceTypeName, cancellationToken);

            ApplyAppParamReplacements(result, application, service);

            if (result.GetValueOrDefault("YARP.EnableDynamicOverrides", null)?.ToLower() == "true")
            {
                // Override with properties
                IDictionary<string, string> properties;
                try
                {
                    properties = await _serviceFabricCaller.EnumeratePropertiesAsync(service.ServiceName, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new ServiceFabricIntegrationException($"Failed to get properties for {service.ServiceName}.", ex);
                }

                OverrideLabels(ref result, properties);
            }

            return result;
        }

        private static void OverrideLabels(ref Dictionary<string, string> labels, IDictionary<string, string> overrides)
        {
            foreach (var entry in overrides)
            {
                if (entry.Key.StartsWith("YARP.", StringComparison.Ordinal))
                {
                    labels[entry.Key] = entry.Value;
                }
            }
        }

        /// <summary>
        /// This class creates XmlReaderSettings providing Safe Xml parsing in the senses below:
        ///     1. DTD processing is disabled to prevent Xml Bomb.
        ///     2. XmlResolver is disabled to prevent Schema/External DTD resolution.
        ///     3. Maximum size for Xml document and entities are explicitly set. Zero for the size means there is no limit.
        ///     4. Comments/processing instructions are not allowed.
        /// </summary>
        private static XmlReaderSettings CreateSafeXmlSetting(long maxAcceptedCharsInDocument, long maxCharactersFromEntities)
        {
            return new XmlReaderSettings
            {
                Async = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = maxAcceptedCharsInDocument,
                MaxCharactersFromEntities = maxCharactersFromEntities,
            };
        }

        /// <summary>
        /// The Service Manifest can specify a label with value <c>[AppParamName]</c>, in which case we replace it
        /// with the value of an application parameter with the given name <c>AppParamName</c>.
        /// Application parameter names are case insensitive in Service Fabric.
        /// If no such app param exists, we replace with empty string.
        /// </summary>
        private void ApplyAppParamReplacements(Dictionary<string, string> labels, ApplicationWrapper app, ServiceWrapper service)
        {
            var replacements = new List<KeyValuePair<string, string>>();
            foreach (var label in labels)
            {
                var value = label.Value;
                if (value.Length > 2 && value[0] == '[' && value[value.Length - 1] == ']')
                {
                    var appParamName = value.Substring(1, value.Length - 2);
                    if (app.ApplicationParameters == null ||
                        !app.ApplicationParameters.TryGetValue(appParamName, out var appParamValue))
                    {
                        // TODO: This should trigger a Warning or Error health report on the faulty service.
                        // This is not critical because if the absence of the setting leads to invalid configs, we *do* already report error
                        // (for example, if a route's rule were missing).
                        _logger.LogInformation($"Application does not specify parameter referenced in a Service Manifest extension label. ApplicationName='{app.ApplicationName}', ApplicationtypeName='{app.ApplicationTypeName}', ApplicationTypeVersion='{app.ApplicationTypeVersion}', ServiceName='{service.ServiceName}', Label='{label.Key}', AppParamName='{appParamName}'.");
                        appParamValue = string.Empty;
                    }

                    replacements.Add(KeyValuePair.Create(label.Key, appParamValue));
                }
            }

            foreach (var replacement in replacements)
            {
                labels[replacement.Key] = replacement.Value;
            }
        }

        /// <summary>
        /// Gets the labels from the extensions of the provided raw service manifest.
        /// </summary>
        private async Task<Dictionary<string, string>> ExtractLabelsAsync(
           string rawServiceManifest,
           string targetServiceTypeName,
           CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var reader = XmlReader.Create(new StringReader(rawServiceManifest), CreateSafeXmlSetting(1024 * 1024, 1024)))
            {
                XDocument parsedManifest;
                try
                {
                    parsedManifest = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
                }
                catch (System.Xml.XmlException ex)
                {
                    // TODO: we don't know if the service wants to use the gateway yet, so not sure if this classifies as config error (considering it will escalate into a bad health report)
                    throw new ConfigException("Failed to parse service manifest XML.", ex);
                }

                // TODO: this is clearly inefficient
                var labels = parsedManifest
                    .Elements(XNSServiceManifest + "ServiceManifest")
                    .Elements(XNSServiceManifest + "ServiceTypes")
                    .Elements().Where(s => (string)s.Attribute("ServiceTypeName") == targetServiceTypeName)
                    .Elements(XNSServiceManifest + "Extensions")
                    .Elements(XNSServiceManifest + "Extension").Where(s => (string)s.Attribute("Name") == ExtensionName)
                    .Elements(XNameLabels)
                    .Elements(XNameLabel);

                foreach (var label in labels)
                {
                    if (!result.TryAdd(label.Attribute("Key").Value, label.Value))
                    {
                        // TODO: we don't know if the service wants to use the gateway yet, so not sure if this classifies as config error (considering it will escalate into a bad health report)
                        throw new ConfigException($"Duplicate label key {label.Attribute("Key").Value} in service manifest extensions.");
                    }
                }
            }
            return result;
        }
    }
}
