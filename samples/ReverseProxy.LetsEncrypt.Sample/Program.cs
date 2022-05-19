using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

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
                webHostBuilder.UseStartup<Startup>();
            });
            var myHost = myHostBuilder.Build();
            myHost.Run();
        }
    }
}
