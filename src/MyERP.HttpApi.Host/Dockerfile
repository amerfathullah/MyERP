FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY common.props .
COPY src/ src/

RUN dotnet restore src/MyERP.HttpApi.Host/MyERP.HttpApi.Host.csproj
RUN dotnet publish src/MyERP.HttpApi.Host/MyERP.HttpApi.Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
LABEL org.opencontainers.image.source="https://github.com/amerfathullah/MyERP"
LABEL org.opencontainers.image.description="MyERP API Server"

RUN apt-get update && apt-get install -y --no-install-recommends curl openssl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
COPY src/MyERP.HttpApi.Host/docker-entrypoint.sh .
RUN chmod +x docker-entrypoint.sh

EXPOSE 5000
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -sf http://localhost:5000/health-status || exit 1

ENTRYPOINT ["./docker-entrypoint.sh"]