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
COPY --from=build /publish ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Waypoint.Api.dll"]
