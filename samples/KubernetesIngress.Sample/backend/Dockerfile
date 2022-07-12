#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0.100-preview.5 AS publish
WORKDIR /samples
COPY ["samples/KubernetesIngress.Sample/backend", "KubernetesIngress.Sample/backend"]

WORKDIR KubernetesIngress.Sample/backend
RUN dotnet publish -c Release -o /app/publish -f net6.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "backend.dll"]
