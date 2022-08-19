#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM amd64/buildpack-deps:jammy-curl AS publish
WORKDIR /src
COPY ["global.json", ""]
RUN sed 's/\r$//' global.json > global-lf.json
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --jsonfile global-lf.json
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT 1

COPY ["samples/KubernetesIngress.Sample/backend", "backend"]

WORKDIR /src/backend
RUN /root/.dotnet/dotnet publish -c Release -o /app/publish -f net6.0

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "backend.dll"]
