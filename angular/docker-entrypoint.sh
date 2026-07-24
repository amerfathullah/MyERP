#!/bin/sh
# MyERP Web — Docker Entrypoint
# Injects runtime environment config (API_URL) into the Angular app at container start.

set -e

API_URL="${API_URL:-http://localhost:5000}"
# Public URL of THIS web app (OAuth redirect target). Defaults to API_URL's not useful,
# so it must be provided in production (e.g. https://erp.mosalah.cloud).
APP_BASE_URL="${APP_BASE_URL:-http://localhost}"

# Write the dynamic environment config that Angular reads at startup
cat > /usr/share/nginx/html/dynamic-env.json <<EOF
{
  "production": true,
  "application": {
    "baseUrl": "${APP_BASE_URL}",
    "name": "MyERP"
  },
  "oAuthConfig": {
    "issuer": "${API_URL}/",
    "redirectUri": "${APP_BASE_URL}",
    "clientId": "MyERP_App",
    "responseType": "code",
    "scope": "offline_access MyERP",
    "requireHttps": false
  },
  "apis": {
    "default": {
      "url": "${API_URL}",
      "rootNamespace": "MyERP"
    },
    "AbpAccountPublic": {
      "url": "${API_URL}/",
      "rootNamespace": "AbpAccountPublic"
    }
  }
}
EOF

echo "[MyERP Web] API_URL=${API_URL}  APP_BASE_URL=${APP_BASE_URL}"
echo "[MyERP Web] Config written to /usr/share/nginx/html/dynamic-env.json"

# Start Nginx
exec nginx -g "daemon off;"
