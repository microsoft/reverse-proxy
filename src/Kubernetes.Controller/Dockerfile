#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /src
COPY ["src/OperatorFramework/src/Controller", "src/OperatorFramework/src/Controller"]
COPY ["src/OperatorFramework/src/Core", "src/OperatorFramework/src/Core"]
COPY ["src/Kubernetes.Controller", "src/Kubernetes.Controller"]
COPY ["src/Kubernetes.Protocol", "src/Kubernetes.Protocol"]
COPY ["src/", "src/"]
COPY ["Directory.Build.*", ""]
COPY ["global.json", ""]
COPY ["NuGet.config", ""]

WORKDIR src/Kubernetes.Controller
RUN dotnet publish -c Release -o /app/publish -f net5.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Yarp.Kubernetes.Controller.dll"]
