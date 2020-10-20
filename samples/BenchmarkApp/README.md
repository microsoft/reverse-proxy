### Crank command to test against a local BenchmarkServer

1. Clone https://github.com/dotnet/crank.git
2. In one shell, `dotnet run` https://github.com/dotnet/crank/tree/master/src/Microsoft.Crank.Agent
3. In another shell, `dotnet run` https://github.com/dotnet/crank/tree/master/src/Microsoft.Crank.Controller as follows: 

```
dotnet run -p ..\..\..\..\dotnet\crank\src\Microsoft.Crank.Controller -- `
     --config https://raw.githubusercontent.com/aspnet/Benchmarks/master/scenarios/proxy.benchmarks.yml `
     --scenario proxy-yarp `
     --no-measurements `
     --load.variables.duration 5 `
     --application.endpoints http://localhost:5010 `
     --load.endpoints http://localhost:5010 `
     --downstream.endpoints http://localhost:5010 `
     --variable serverAddress=localhost `
     --variable serverPort=5000 `
     --variable downstreamAddress=localhost `
     --variable downstreamPort=5001 `
     --variable path=/?s=1024 `
     --variable serverScheme=https `
     --variable downstreamScheme=https `
     --load.variables.transport http2 `
     --downstream.variables.httpProtocol http2
```
