FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY common.props .
COPY src/ src/

RUN dotnet restore src/MyERP.DbMigrator/MyERP.DbMigrator.csproj
RUN dotnet publish src/MyERP.DbMigrator/MyERP.DbMigrator.csproj -c Release -o /app/publish --no-restore

# ASP.NET Core runtime — the migrator pulls in ABP packages that depend on
# Microsoft.AspNetCore.App, so the plain dotnet/runtime image can't launch it.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyERP.DbMigrator.dll"]