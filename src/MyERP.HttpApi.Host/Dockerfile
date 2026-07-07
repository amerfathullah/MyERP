FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY common.props .
COPY MyERP.slnx .
COPY src/ src/

RUN dotnet restore MyERP.slnx
RUN dotnet publish src/MyERP.HttpApi.Host/MyERP.HttpApi.Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "MyERP.HttpApi.Host.dll"]