# T212Ext web app. Built by docker-compose.yml on the server; see docs/DEPLOYMENT.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so a code change does not repeat it.
COPY nuget.config Directory.Build.props VERSION ./
COPY src/T212Ext.Client/T212Ext.Client.csproj src/T212Ext.Client/
COPY src/T212Ext.Core/T212Ext.Core.csproj src/T212Ext.Core/
COPY src/T212Ext.Web/T212Ext.Web.csproj src/T212Ext.Web/
RUN dotnet restore src/T212Ext.Web/T212Ext.Web.csproj

COPY src/ src/
RUN dotnet publish src/T212Ext.Web/T212Ext.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl for the health check; tzdata so dates show in Lisbon time (TZ below).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl tzdata \
    && rm -rf /var/lib/apt/lists/*

# Data protection keys (sign-in and language cookies) live on a volume mounted here; a new
# named volume takes this folder's owner, so the non-root user can write to it.
RUN mkdir -p /data/keys && chown "$APP_UID" /data/keys

WORKDIR /app
COPY --from=build /app .

# The image listens on 8080 (ASPNETCORE_HTTP_PORTS) as the non-root "app" user.
ENV TZ=Europe/Lisbon \
    DataProtection__KeysPath=/data/keys
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "T212Ext.Web.dll"]
