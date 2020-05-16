// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace BenchmarkApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WriteStatistics();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        private static void WriteStatistics()
        {
            Console.WriteLine("#StartJobStatistics");

            Console.WriteLine(JsonSerializer.Serialize(new {
                Metadata = new object[]
                {
                    new { Source= "Benchmarks", Name= "AspNetCoreVersion", ShortDescription = "ASP.NET Core Version", LongDescription = "ASP.NET Core Version" },
                    new { Source= "Benchmarks", Name= "NetCoreAppVersion", ShortDescription = ".NET Runtime Version", LongDescription = ".NET Runtime Version" },
                },
                Measurements = new object[]
                {
                    new { Timestamp = DateTime.UtcNow, Name = "AspNetCoreVersion", Value = typeof(WebHostBuilder).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion },
                    new { Timestamp = DateTime.UtcNow, Name = "NetCoreAppVersion", Value = typeof(object).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion },
                }
            }));

            Console.WriteLine("#EndJobStatistics");
        }
    }
}
