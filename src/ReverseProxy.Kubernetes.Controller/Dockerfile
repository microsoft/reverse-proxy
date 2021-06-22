#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS publish
WORKDIR /src
COPY ["src/OperatorFramework/src/Controller", "OperatorFramework/src/Controller"]
COPY ["src/OperatorFramework/src/Core", "OperatorFramework/src/Core"]
COPY ["src/ReverseProxy.Kubernetes.Controller", "ReverseProxy.Kubernetes.Controller"]
COPY ["src/ReverseProxy.Kubernetes.Protocol", "ReverseProxy.Kubernetes.Protocol"]
COPY ["src/ReverseProxy", "ReverseProxy"]

WORKDIR ReverseProxy.Kubernetes.Controller
RUN dotnet publish -c Release -o /app/publish -f net5.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Yarp.ReverseProxy.Kubernetes.Controller.dll"]
