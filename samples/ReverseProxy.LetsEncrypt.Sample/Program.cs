using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Yarp.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Create a Kestrel web server, and tell it to use the Startup class
            // for the service configuration
            var myHostBuilder = Host.CreateDefaultBuilder(args);
            myHostBuilder.ConfigureWebHostDefaults(webHostBuilder =>
            {
                webHostBuilder.ConfigureKestrel(kestrel =>
                {
                    kestrel.ListenAnyIP(443, portOptions =>
                    {;
                        portOptions.UseHttps(h =>
                        {
                            h.UseLettuceEncrypt(kestrel.ApplicationServices);
                        });
                    });
                });
                webHostBuilder.UseStartup<Startup>();
            });
            var myHost = myHostBuilder.Build();
            myHost.Run();
        }
    }
}
