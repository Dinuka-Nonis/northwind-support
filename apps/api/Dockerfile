# Build from the repository root, because that is the build context:
#   docker build -f apps/api/Dockerfile -t supportdesk-api .
# (docker compose does this for you, see docker-compose.yml.)

# ---- Build stage: the full .NET 10 SDK, thrown away after publishing -------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. The files that decide how every project is restored and built. They change rarely, so this
#    layer is almost always cached.
COPY global.json Directory.Build.props Directory.Packages.props ./

# 2. Only the project files of the four layers, each into its own folder, which is all `restore`
#    needs. The test projects are never copied: the image does not build or run them.
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/

# 3. Restore the Presentation project, which pulls in the other three. This layer is rebuilt only
#    when a project file or a package version changes, not when a .cs file does.
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# 4. Now the source. Changing a .cs file invalidates only the layers from here down.
COPY apps/api/src/ apps/api/src/

# 5. Publish without restoring again. UseAppHost=false skips the native launcher, which the image
#    does not need because it starts the app with `dotnet <dll>`.
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    -c Release --no-restore -o /app /p:UseAppHost=false

# ---- Runtime stage: the much smaller ASP.NET runtime image -----------------------------------
# The standard (Debian) image, not chiseled or distroless: it still has a shell and `whoami`, so
# the container can be inspected with `docker compose exec`.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

# Plain HTTP on 8080. TLS is terminated in front of the container, so there are no certificates
# here. This is the only ENV in the image, and it is a port, not configuration or a secret.
# The connection string, the JWT key and the environment name all arrive as environment
# variables when the container starts (see docker-compose.yml and .env.example).
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# `app` is the non-root user that ships in the .NET images. Port 8080 needs no privileges.
USER app

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
