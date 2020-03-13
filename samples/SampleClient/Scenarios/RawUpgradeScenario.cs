// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SampleClient.Scenarios
{
    internal class RawUpgradeScenario : IScenario
    {
        public async Task ExecuteAsync(CommandLineArgs args, CancellationToken cancellation)
        {
            using (var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false,
            })
            using (var client = new HttpMessageInvoker(handler))
            {
                var targetUri = new Uri(new Uri(args.Target, UriKind.Absolute), "api/rawupgrade");
                var stopwatch = Stopwatch.StartNew();
                var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
                request.Headers.TryAddWithoutValidation("Connection", "upgrade");
                request.Version = new Version(1, 1);
                Console.WriteLine($"Calling {targetUri} with upgradable HTTP/1.1");

                var response = await client.SendAsync(request, cancellation);
                Console.WriteLine($"Received response: {(int)response.StatusCode} in {stopwatch.ElapsedMilliseconds} ms");
                if (response.StatusCode != HttpStatusCode.SwitchingProtocols)
                {
                    throw new InvalidOperationException($"Expected status 101 Switching Protocols!");
                }

                var rawStream = await response.Content.ReadAsStreamAsync();
                Console.WriteLine("Acquired upgraded stream. Testing bidirectional echo...");
                stopwatch.Restart();
                var buffer = new byte[1];
                for (var i = 0; i <= 255; i++)
                {
                    buffer[0] = (byte)i;
                    await rawStream.WriteAsync(buffer, 0, 1);
                    var read = await rawStream.ReadAsync(buffer);
                    if (i == 255)
                    {
                        if (read != 0)
                        {
                            throw new Exception($"Read {read} bytes, expected 0 after sending Goodbye.");
                        }

                        Console.WriteLine();
                    }
                    else
                    {
                        if (read != 1)
                        {
                            throw new Exception($"Read {read} bytes, expected 1.");
                        }
                        if (buffer[0] != i)
                        {
                            throw new Exception($"Received {buffer[0]}, expected {i}.");
                        }

                        Console.Write(".");
                    }
                }

                Console.WriteLine($"256 ping/pong's completed in {stopwatch.ElapsedMilliseconds} ms.");
            }
        }
    }
}
