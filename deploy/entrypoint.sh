#!/bin/bash
set -e

# Default to port 10000 if PORT is not provided by Render
PORT="${PORT:-10000}"
export PORT

echo "=== Starting BestieSpace on Render (Port: ${PORT}) ==="

# Substitute ${PORT} in nginx configuration
envsubst '${PORT}' < /etc/nginx/nginx.conf.template > /etc/nginx/nginx.conf

# Setup data directory for persistent SQLite and uploads
DATA_DIR="${DATA_DIR:-/app/data}"
mkdir -p "${DATA_DIR}/uploads/memories"
mkdir -p "${DATA_DIR}/uploads/chat-photos"
mkdir -p "${DATA_DIR}/uploads/voice-notes"
chmod -R 777 "${DATA_DIR}" || true

# Symlink uploads into API wwwroot so static file middleware serves them
mkdir -p /app/api/wwwroot
if [ ! -L /app/api/wwwroot/uploads ]; then
    rm -rf /app/api/wwwroot/uploads
    ln -s "${DATA_DIR}/uploads" /app/api/wwwroot/uploads
fi

# 1. Start BestieSpace.Api in background on internal port 5000
echo "Starting BestieSpace.Api on internal port 5000..."
cd /app/api
export DATA_DIR="${DATA_DIR}"
dotnet BestieSpace.Api.dll --urls "http://127.0.0.1:5000" &
API_PID=$!

# Wait for API to respond
echo "Waiting for BestieSpace.Api to initialize database..."
for i in {1..30}; do
    if curl -s http://127.0.0.1:5000/api/dashboard > /dev/null 2>&1; then
        echo "BestieSpace.Api is ready and database is initialized!"
        break
    fi
    sleep 1
done

# 2. Start BestieSpace.Client in background on internal port 5100
echo "Starting BestieSpace.Client on internal port 5100..."
cd /app/client
export ApiBaseUrl="http://127.0.0.1:5000"
dotnet BestieSpace.Client.dll --urls "http://127.0.0.1:5100" &
CLIENT_PID=$!

# 3. Start Nginx in foreground to route external traffic on ${PORT}
echo "Starting Nginx reverse proxy on external port ${PORT}..."
nginx -g "daemon off;"
