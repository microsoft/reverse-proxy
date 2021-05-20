#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["samples/KuberenetesIngress.Sample/Ingress/Yarp.ReverseProxy.Kubernetes.Ingress.csproj", "samples/KuberenetesIngress.Sample/Ingress/"]
COPY ["src/ReverseProxy/Yarp.ReverseProxy.csproj", "src/ReverseProxy/"]
COPY ["src/OperatorFramework/src/Controller/Microsoft.Kubernetes.Controller.csproj", "src/OperatorFramework/src/Controller/"]
COPY ["src/OperatorFramework/src/Core/Microsoft.Kubernetes.Core.csproj", "src/OperatorFramework/src/Core/"]
COPY ["src/ReverseProxy.Kubernetes.Protocol/Yarp.ReverseProxy.Kubernetes.Protocol.csproj", "src/ReverseProxy.Kubernetes.Protocol/"]
RUN dotnet restore "samples/KuberenetesIngress.Sample/Ingress/Yarp.ReverseProxy.Kubernetes.Ingress.csproj"
COPY . .
# WORKDIR /samples/KuberenetesIngress.Sample/Ingress/
RUN dotnet build "samples/KuberenetesIngress.Sample/Ingress/Yarp.ReverseProxy.Kubernetes.Ingress.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Yarp.ReverseProxy.Kubernetes.Ingress.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ingress.dll"]
