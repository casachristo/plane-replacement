# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Waypoint.Api/Waypoint.Api.csproj          src/Waypoint.Api/
COPY src/Waypoint.Domain/Waypoint.Domain.csproj    src/Waypoint.Domain/
COPY src/Waypoint.Contracts/Waypoint.Contracts.csproj src/Waypoint.Contracts/
RUN dotnet restore src/Waypoint.Api/Waypoint.Api.csproj
COPY src/ src/
RUN dotnet publish src/Waypoint.Api/Waypoint.Api.csproj -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
# ca-certificates package + ability to update the trust store at startup. The Helm
# chart mounts the homelab CA into /usr/local/share/ca-certificates/; the entrypoint
# runs update-ca-certificates once before launching the app.
RUN apk add --no-cache ca-certificates
COPY --from=build /publish ./
COPY <<'EOF' /app/entrypoint.sh
#!/bin/sh
set -e
if [ -d /usr/local/share/ca-certificates ] && [ "$(ls -A /usr/local/share/ca-certificates 2>/dev/null)" ]; then
  update-ca-certificates >/dev/null 2>&1 || true
fi
exec dotnet Waypoint.Api.dll "$@"
EOF
RUN chmod +x /app/entrypoint.sh
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["/app/entrypoint.sh"]
