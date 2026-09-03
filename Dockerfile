# syntax=docker/dockerfile:1
#
# Builds UMS.Host - the single ums-core deployable (ADR-0001) hosting all 18 modules behind one
# ASP.NET Core pipeline. Unlike kart-commerce's per-service Dockerfiles (which COPY only their own
# service's .csproj files for a tight build-cache layer), this monolith COPYs the whole solution:
# with 18 module projects all feeding one published output, per-project COPY lines stop scaling
# and .dockerignore already keeps bin/obj/tests/docs out of the build context.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,id=ums-core-nuget-packages \
    dotnet restore src/Host/UMS.Host.csproj

RUN --mount=type=cache,target=/root/.nuget/packages,id=ums-core-nuget-packages \
    dotnet publish src/Host/UMS.Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is what docker-compose's own healthcheck (and a kubelet exec probe, if ever used instead of
# the httpGet probe) calls against /health/live - not present in the base image by default.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Runs as the image's built-in unprivileged "app" user, not root - the podSecurityContext every
# real deployment (ums-infra's service-chart) requires (runAsNonRoot: true).
USER $APP_UID

ENTRYPOINT ["dotnet", "UMS.Host.dll"]
