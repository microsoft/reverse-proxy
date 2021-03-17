#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["controller/src/Default/Ingress.csproj", "controller/src/Default/"]
COPY ["src/ReverseProxy/Microsoft.ReverseProxy.csproj", "src/ReverseProxy/"]
COPY ["controller/src/OperatorFramework/src/Controller/Microsoft.Kubernetes.Controller.csproj", "controller/src/OperatorFramework/src/Controller/"]
COPY ["controller/src/OperatorFramework/src/Core/Microsoft.Kubernetes.Core.csproj", "controller/src/OperatorFramework/src/Core/"]
COPY ["controller/src/WatchingServicesProtocol/WatchingServicesProtocol.csproj", "controller/src/WatchingServicesProtocol/"]
RUN dotnet restore "controller/src/Default/Ingress.csproj"
COPY . .
WORKDIR "/src/controller/src/Default"
RUN dotnet build "Ingress.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Ingress.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Ingress.dll"]
