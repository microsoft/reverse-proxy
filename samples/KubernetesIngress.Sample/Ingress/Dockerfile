#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0.100-preview.3 AS publish
WORKDIR /src

# Copy csproj files and other files needed for restoring (to build a nuget cache layer to speed up rebuilds)
COPY ["samples/KubernetesIngress.Sample/Ingress/Yarp.Kubernetes.Ingress.csproj", "samples/KubernetesIngress.Sample/Ingress/"]
COPY ["src/ReverseProxy/Yarp.ReverseProxy.csproj", "src/ReverseProxy/"]
COPY ["src/Kubernetes.Controller/Yarp.Kubernetes.Controller.csproj", "src/Kubernetes.Controller/"]
COPY ["src/Directory.Build.props", "src/"]
COPY ["Directory.Build.*", "./"]
COPY ["global.json", ""]
COPY ["NuGet.config", ""]

# Build a cache layer with all of the nuget packages
RUN dotnet restore samples/KubernetesIngress.Sample/Ingress/Yarp.Kubernetes.Ingress.csproj

# Copy the remaining source files
WORKDIR /src
COPY . .

WORKDIR /src/samples/KubernetesIngress.Sample/Ingress/
RUN dotnet publish -c Release --no-restore -o /app/publish -f net6.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Yarp.Kubernetes.Ingress.dll"]
