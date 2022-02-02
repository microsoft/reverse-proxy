#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /src

# Copy csproj files and other files needed for restoring (to build a nuget cache layer to speed up rebuilds)
COPY ["samples/KuberenetesIngress.Sample/Ingress/Yarp.Kubernetes.Ingress.csproj", "samples/KuberenetesIngress.Sample/Ingress/"]
COPY ["src/OperatorFramework/src/Controller/Microsoft.Kubernetes.Controller.csproj", "src/OperatorFramework/src/Controller/"]
COPY ["src/OperatorFramework/src/Core/Microsoft.Kubernetes.Core.csproj", "src/OperatorFramework/src/Core/"]
COPY ["src/ReverseProxy/Yarp.ReverseProxy.csproj", "src/ReverseProxy/"]
COPY ["src/Kubernetes.Protocol/Yarp.Kubernetes.Protocol.csproj", "src/Kubernetes.Protocol/"]
COPY ["src/Directory.Build.props", "src/"]
COPY ["Directory.Build.*", "./"]
COPY ["global.json", ""]
COPY ["NuGet.config", ""]

# Build a cache layer with all of the nuget packages
RUN dotnet restore samples/KuberenetesIngress.Sample/Ingress/Yarp.Kubernetes.Ingress.csproj

# Copy the remaining source files
WORKDIR /src
COPY . .

WORKDIR /src/samples/KuberenetesIngress.Sample/Ingress/
RUN dotnet publish -c Release --no-restore -o /app/publish -f net6.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Yarp.Kubernetes.Ingress.dll"]
