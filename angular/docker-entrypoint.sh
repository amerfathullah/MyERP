#!/bin/sh
# MyERP Web — Docker Entrypoint
# Injects runtime environment config (API_URL) into the Angular app at container start.

set -e

API_URL="${API_URL:-http://localhost:5000}"

# Write the dynamic environment config that Angular reads at startup
cat > /usr/share/nginx/html/dynamic-env.json <<EOF
{
  "production": true,
  "application": {
    "name": "MyERP"
  },
  "oAuthConfig": {
    "issuer": "${API_URL}/",
    "redirectUri": "{0}",
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

echo "[MyERP Web] API_URL=${API_URL}"
echo "[MyERP Web] Config written to /usr/share/nginx/html/dynamic-env.json"

# Start Nginx
exec nginx -g "daemon off;"
