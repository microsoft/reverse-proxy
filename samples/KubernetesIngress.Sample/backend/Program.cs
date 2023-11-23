// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;

Activity.DefaultIdFormat = ActivityIdFormat.W3C;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", async context =>
{
    var backendInfo = new BackendInfo()
    {
        IP = context.Connection.LocalIpAddress.ToString(),
        Hostname = Dns.GetHostName(),
    };

    context.Response.ContentType = "application/json; charset=utf-8";
    await JsonSerializer.SerializeAsync(context.Response.Body, backendInfo);
});

app.Run();

internal class BackendInfo
{
    public string IP { get; set; } = default!;

    public string Hostname { get; set; } = default!;
}
