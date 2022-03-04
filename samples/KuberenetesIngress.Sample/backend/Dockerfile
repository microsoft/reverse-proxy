#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0.100 AS publish
WORKDIR /samples
COPY ["samples/KuberenetesIngress.Sample/backend", "KuberenetesIngress.Sample/backend"]

WORKDIR KuberenetesIngress.Sample/backend
RUN dotnet publish -c Release -o /app/publish -f net6.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "backend.dll"]
