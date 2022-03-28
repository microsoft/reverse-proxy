// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes.ResourceKindProvider.OpenApi;
using NJsonSchema;
using NSwag;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.ResourceKinds.OpenApi;

public class OpenApiResourceKindProvider : IResourceKindProvider
{
    private readonly Dictionary<(string apiVersion, string kind), OpenApiResourceKind> _resourceKinds = new Dictionary<(string apiVersion, string kind), OpenApiResourceKind>();
    private readonly object _resourceKindsSync = new object();
    private readonly Lazy<Task<IDictionary<string, JsonSchema>>> _lazyDefinitions;
    private readonly Lazy<Task<ApiVersionKindSchemasDictionary>> _lazyApiVersionKindSchemas;
    private readonly ILogger<OpenApiResourceKindProvider> _logger;

    public OpenApiResourceKindProvider(ILogger<OpenApiResourceKindProvider> logger)
    {
        _lazyDefinitions = new Lazy<Task<IDictionary<string, JsonSchema>>>(LoadDefinitions, LazyThreadSafetyMode.ExecutionAndPublication);
        _lazyApiVersionKindSchemas = new Lazy<Task<ApiVersionKindSchemasDictionary>>(LoadApiVersionKindSchemas, LazyThreadSafetyMode.ExecutionAndPublication);
        _logger = logger;
    }

    public async Task<IResourceKind> GetResourceKindAsync(string apiVersion, string kind)
    {
        var key = (apiVersion, kind);
        lock (_resourceKindsSync)
        {
            if (_resourceKinds.TryGetValue(key, out var cachedResourceKind))
            {
                return cachedResourceKind;
            }
        }

        var apiVersionKindSchemas = await _lazyApiVersionKindSchemas.Value;
        if (!apiVersionKindSchemas.TryGetValue(key, out var schema))
        {
            return null;
        }

        var resourceKind = new OpenApiResourceKind(apiVersion, kind, schema);

        lock (_resourceKindsSync)
        {
            if (!_resourceKinds.TryAdd(key, resourceKind))
            {
                resourceKind = _resourceKinds[key];
            }
        }
        return resourceKind;
    }

    public async Task<IDictionary<string, JsonSchema>> LoadDefinitions()
    {
        using var stream = typeof(OpenApiResourceKindProvider).Assembly.GetManifestResourceStream(typeof(OpenApiResourceKindProvider), "swagger.json");
        if (stream is null)
        {
            _logger.LogError(
                new EventId(1, "MissingEmbeddedStream"),
                "Assembly {AssemblyName} does not contain embedded stream {EmbeddedNamespace}.{EmbeddedName}",
                typeof(OpenApiResourceKindProvider).Assembly.GetName().Name,
                typeof(OpenApiResourceKindProvider).FullName,
                "swagger.json");

            throw new FileNotFoundException(
                "Embedded stream not found",
                $"{typeof(OpenApiResourceKindProvider).FullName}.swagger.json");
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var openApiDocument = await OpenApiDocument.FromJsonAsync(json);
        return openApiDocument.Definitions;
    }

    private async Task<ApiVersionKindSchemasDictionary> LoadApiVersionKindSchemas()
    {
        var definitions = await _lazyDefinitions.Value;

        var schemas = new ApiVersionKindSchemasDictionary();

        foreach (var (_, definition) in definitions)
        {
            if (definition.ExtensionData?.TryGetValue("x-kubernetes-group-version-kind", out var _) ?? false)
            {
                var groupVersionKindElements = (object[])definition.ExtensionData["x-kubernetes-group-version-kind"];
                var groupVersionKind = (Dictionary<string, object>)groupVersionKindElements[0];

                var group = (string)groupVersionKind["group"];
                var version = (string)groupVersionKind["version"];
                var kind = (string)groupVersionKind["kind"];

                if (string.IsNullOrEmpty(group))
                {
                    schemas[(version, kind)] = definition;
                }
                else
                {
                    schemas[($"{group}/{version}", kind)] = definition;
                }
            }
        }

        return schemas;
    }

    internal sealed class ApiVersionKindSchemasDictionary : Dictionary<(string apiVersion, string kind), JsonSchema>
    {
    }
}
