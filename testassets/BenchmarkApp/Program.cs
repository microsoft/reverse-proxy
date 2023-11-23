// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Crank.EventSources;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((context, kestrelOptions) =>
{
    kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.ServerCertificate = new X509Certificate2(Path.Combine(context.HostingEnvironment.ContentRootPath, "testCert.pfx"), "testPassword");
    });
});

var clusterUrls = builder.Configuration["clusterUrls"];

if (string.IsNullOrWhiteSpace(clusterUrls))
{
    throw new ArgumentException("--clusterUrls is required");
}

var configDictionary = new Dictionary<string, string>
{
    { "Routes:route:ClusterId", "cluster" },
    { "Routes:route:Match:Path", "/{**catchall}" },
    { "Clusters:cluster:HttpClient:DangerousAcceptAnyServerCertificate", "true" },
};

var clusterCount = 0;
foreach (var clusterUrl in clusterUrls.Split(';'))
{
    configDictionary.Add($"Clusters:cluster:Destinations:destination{clusterCount++}:Address", clusterUrl);
}

var proxyConfig = new ConfigurationBuilder().AddInMemoryCollection(configDictionary).Build();

builder.Services.AddReverseProxy().LoadFromConfig(proxyConfig);

var app = builder.Build();

BenchmarksEventSource.MeasureAspNetVersion();
BenchmarksEventSource.MeasureNetCoreAppVersion();

// Register the reverse proxy routes
app.MapReverseProxy(builder =>
{
    // Skip SessionAffinity, LoadBalancing and PassiveHealthChecks
});

app.Run();
