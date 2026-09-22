# CashRegister API. Build from the repository root (the build needs global.json and the shared
# Directory.*.props files):
#   docker build -f src/CashRegister.Api/Dockerfile -t cashregister-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore first, from project files only, so the restore layer is cached until dependencies change.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/CashRegister/CashRegister.csproj src/CashRegister/
COPY src/CashRegister.Api/CashRegister.Api.csproj src/CashRegister.Api/
RUN dotnet restore src/CashRegister.Api/CashRegister.Api.csproj

COPY src/ src/
RUN dotnet publish src/CashRegister.Api/CashRegister.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The aspnet image listens on 8080 and ships a non-root "app" user.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CashRegister.Api.dll"]
