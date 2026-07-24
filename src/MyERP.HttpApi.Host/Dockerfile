FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Node.js + Yarn + ABP CLI — required to fetch client-side libs (wwwroot/libs) that the
# server-rendered LeptonX account/login pages need in order to render.
RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && npm install -g yarn \
    && dotnet tool install -g Volo.Abp.Studio.Cli \
    && rm -rf /var/lib/apt/lists/*
ENV PATH="${PATH}:/root/.dotnet/tools"

COPY common.props .
COPY src/ src/

RUN dotnet restore src/MyERP.HttpApi.Host/MyERP.HttpApi.Host.csproj

# Populate wwwroot/libs (bootstrap, jquery, etc.) before publish so it ships in the image.
RUN cd src/MyERP.HttpApi.Host && abp install-libs

RUN dotnet publish src/MyERP.HttpApi.Host/MyERP.HttpApi.Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
LABEL org.opencontainers.image.source="https://github.com/myerp/myerp"
LABEL org.opencontainers.image.description="MyERP API Server"

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -sf http://localhost:5000/health-status || exit 1

ENTRYPOINT ["dotnet", "MyERP.HttpApi.Host.dll"]
