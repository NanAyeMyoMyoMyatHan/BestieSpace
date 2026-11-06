# ===================================================================
# STAGE 1: Build & Publish the .NET Solution
# ===================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project definitions for efficient layer caching
COPY BestieSpace.sln ./
COPY BestieSpace.Shared/BestieSpace.Shared.csproj BestieSpace.Shared/
COPY BestieSpace.Api/BestieSpace.Api.csproj BestieSpace.Api/
COPY BestieSpace.Client/BestieSpace.Client.csproj BestieSpace.Client/

# Restore NuGet packages
RUN dotnet restore BestieSpace.sln

# Copy the entire source tree
COPY . .

# Publish BestieSpace.Api
RUN dotnet publish BestieSpace.Api/BestieSpace.Api.csproj -c Release -o /app/api --no-restore

# Publish BestieSpace.Client
RUN dotnet publish BestieSpace.Client/BestieSpace.Client.csproj -c Release -o /app/client --no-restore

# ===================================================================
# STAGE 2: Production Runtime with Nginx Reverse Proxy
# ===================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install Nginx, curl, and gettext-base (for envsubst)
RUN apt-get update \
    && apt-get install -y --no-install-recommends nginx gettext-base curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published artifacts from build stage
COPY --from=build /app/api /app/api
COPY --from=build /app/client /app/client

# Copy deployment configurations
COPY deploy/nginx.conf.template /etc/nginx/nginx.conf.template
COPY deploy/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Setup base folders and permissions
RUN mkdir -p /app/data/uploads/memories \
    && mkdir -p /app/data/uploads/chat-photos \
    && mkdir -p /app/data/uploads/voice-notes \
    && chmod -R 777 /app/data

# Environment configuration
ENV PORT=10000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATA_DIR=/app/data

EXPOSE 10000

ENTRYPOINT ["/app/entrypoint.sh"]
