#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /src
COPY ["src/OperatorFramework/src/Controller", "src/OperatorFramework/src/Controller"]
COPY ["src/OperatorFramework/src/Core", "src/OperatorFramework/src/Core"]
COPY ["src/ReverseProxy", "src/ReverseProxy"]
COPY ["src/ReverseProxy.Kubernetes.Protocol", "src/ReverseProxy.Kubernetes.Protocol"]
COPY ["src/", "src/"]
COPY ["Directory.Build.*", ""]
COPY ["global.json", ""]
COPY ["NuGet.config", ""]

WORKDIR /src
COPY ["samples/KuberenetesIngress.Sample/Ingress", "samples/KuberenetesIngress.Sample/Ingress"]

WORKDIR samples/KuberenetesIngress.Sample/Ingress/
RUN dotnet publish -c Release -o /app/publish -f net6.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Yarp.Kubernetes.Ingress.dll"]
