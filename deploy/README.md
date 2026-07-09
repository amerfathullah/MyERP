# MyERP — Self-Hosting Guide

Deploy MyERP on your own server in minutes. Similar to ERPNext's self-hosted deployment, MyERP runs entirely in Docker containers.

---

## Quick Install (One Command)

```bash
curl -sL https://raw.githubusercontent.com/myerp/myerp/main/MyERP/deploy/install.sh | bash
```

This will:
1. Download `docker-compose.yml` and `.env`
2. Generate a secure database password
3. Pull images from Docker Hub
4. Start all services
5. Run database migrations automatically

**Default login:** `admin` / `1q2w3E*`

---

## Manual Install

### 1. Create a directory

```bash
mkdir myerp && cd myerp
```

### 2. Download the compose file

```bash
curl -sL https://raw.githubusercontent.com/myerp/myerp/main/MyERP/deploy/docker-compose.yml -o docker-compose.yml
curl -sL https://raw.githubusercontent.com/myerp/myerp/main/MyERP/deploy/.env.example -o .env
```

### 3. Configure

Edit `.env` with your settings:

```bash
nano .env
```

Key settings:
| Variable | Description | Default |
|----------|-------------|---------|
| `DB_PASSWORD` | PostgreSQL password | (auto-generated) |
| `APP_URL` | Public API URL | `http://localhost:5000` |
| `WEB_URL` | Public web URL | `http://localhost` |
| `HTTP_PORT` | Port for the web app | `80` |
| `MYERP_VERSION` | Image version tag | `latest` |

### 4. Start

```bash
docker compose up -d
```

### 5. Access

- **Web App**: http://localhost (or your configured domain)
- **API/Swagger**: http://localhost:5000/swagger
- **Login**: `admin` / `1q2w3E*`

---

## Architecture

```
┌─────────────────────────────────────────────────┐
│                    Host Machine                   │
├─────────────────────────────────────────────────┤
│                                                   │
│  ┌──────────┐  ┌──────────┐  ┌──────────────┐  │
│  │  Web     │  │   API    │  │   Migrator   │  │
│  │ (Nginx)  │  │  (.NET)  │  │  (runs once) │  │
│  │  :80     │  │  :5000   │  │              │  │
│  └────┬─────┘  └────┬─────┘  └──────────────┘  │
│       │              │                           │
│  ┌────┴──────────────┴───────────────────────┐  │
│  │            Internal Network                │  │
│  ├───────────────────┬───────────────────────┤  │
│  │                   │                        │  │
│  │  ┌────────────┐  │  ┌────────────────┐   │  │
│  │  │ PostgreSQL │  │  │     Redis      │   │  │
│  │  │   :5432    │  │  │     :6379      │   │  │
│  │  └────────────┘  │  └────────────────┘   │  │
│  └───────────────────┴───────────────────────┘  │
└─────────────────────────────────────────────────┘
```

**Images (Docker Hub):**
| Image | Purpose | Size |
|-------|---------|------|
| `myerp/api` | .NET 10 API server | ~200MB |
| `myerp/web` | Angular app on Nginx | ~30MB |
| `myerp/migrator` | Database migrations & seed | ~180MB |

---

## Updates

```bash
cd myerp

# Pull latest images
docker compose pull

# Restart (migrator runs automatically on start)
docker compose up -d
```

To pin a specific version:
```bash
# In .env
MYERP_VERSION=1.2.0
```

---

## Backup & Restore

### Backup

```bash
# Database
docker compose exec db pg_dump -U myerp MyERP | gzip > backup_$(date +%Y%m%d).sql.gz

# Full data volumes
docker run --rm -v myerp_myerp_pgdata:/data -v $(pwd):/backup alpine tar czf /backup/pgdata.tar.gz -C /data .
```

### Restore

```bash
# Database
gunzip < backup_20260709.sql.gz | docker compose exec -T db psql -U myerp MyERP
```

### Automated Daily Backup

```bash
# Add to crontab (crontab -e)
0 2 * * * cd /path/to/myerp && docker compose exec -T db pg_dump -U myerp MyERP | gzip > backups/daily_$(date +\%Y\%m\%d).sql.gz
```

---

## HTTPS / Custom Domain

### Option A: Reverse Proxy (Recommended)

Put MyERP behind Nginx, Caddy, or Traefik on your host:

```bash
# .env
APP_URL=https://api.myerp.yourdomain.com
WEB_URL=https://myerp.yourdomain.com
REQUIRE_HTTPS=true
HTTP_PORT=8080  # Internal port, reverse proxy handles 443
```

**Caddy example** (`/etc/caddy/Caddyfile`):
```
myerp.yourdomain.com {
    reverse_proxy localhost:8080
}

api.myerp.yourdomain.com {
    reverse_proxy localhost:5000
}
```

### Option B: Single Domain (API + Web on same host)

If your API is at `https://yourdomain.com/api`:
```bash
APP_URL=https://yourdomain.com
WEB_URL=https://yourdomain.com
```

---

## Scaling

### Multiple API Instances

```bash
docker compose up -d --scale api=3
```

Add a load balancer (Nginx/Traefik) in front of the API containers.

### External Database

```bash
# .env — point to external PostgreSQL
DB_PASSWORD=...  # Not used when overriding connection string

# docker-compose.override.yml
services:
  api:
    environment:
      ConnectionStrings__Default: "Host=your-rds-endpoint.amazonaws.com;Port=5432;Database=MyERP;Username=myerp;Password=YOUR_PASSWORD"
  migrator:
    environment:
      ConnectionStrings__Default: "Host=your-rds-endpoint.amazonaws.com;Port=5432;Database=MyERP;Username=myerp;Password=YOUR_PASSWORD"
  db:
    profiles: ["disabled"]  # Don't start local DB
```

---

## Configuration

### Environment Variables (API)

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Redis__Configuration` | Redis connection string |
| `App__SelfUrl` | Public URL of the API |
| `App__CorsOrigins` | Allowed CORS origins (comma-separated) |
| `AuthServer__Authority` | OAuth issuer URL (same as App__SelfUrl) |
| `AuthServer__RequireHttpsMetadata` | Require HTTPS for OAuth metadata |
| `LHDN__BaseUrl` | LHDN MyInvois API URL |
| `LHDN__ClientId` | LHDN API client ID |
| `LHDN__ClientSecret` | LHDN API client secret |
| `SMTP__Host` | SMTP server for emails |

### Environment Variables (Web)

| Variable | Description |
|----------|-------------|
| `API_URL` | Backend API URL (injected at container start) |

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **Port 80 already in use** | Change `HTTP_PORT` in `.env` to 8080 or another free port |
| **Cannot connect to database** | Ensure `DB_PASSWORD` matches between services |
| **Login fails** | Run migrator again: `docker compose run --rm migrator` |
| **API unhealthy** | Check logs: `docker compose logs api` |
| **Blank page on web** | Verify `API_URL` in `.env` is reachable from browser |
| **CORS errors** | Ensure `WEB_URL` in `.env` matches the URL in your browser |
| **Migrator keeps restarting** | It should exit after completion; check `docker compose logs migrator` |
| **Out of disk space** | Prune old images: `docker system prune -a` |

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f api
docker compose logs -f web
docker compose logs migrator
```

### Reset Everything

```bash
docker compose down -v  # ⚠️ Destroys all data!
docker compose up -d    # Fresh start
```

---

## System Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| RAM | 2 GB | 4 GB |
| CPU | 2 cores | 4 cores |
| Disk | 10 GB | 50 GB |
| OS | Any with Docker support | Ubuntu 22.04+ / Debian 12+ |

---

## Comparison with ERPNext

| Feature | MyERP | ERPNext |
|---------|-------|---------|
| Install method | `docker compose up` | bench/Docker |
| Backend | .NET 10 (compiled, fast) | Python/Frappe |
| Frontend | Angular SPA | Frappe UI (jQuery + Vue) |
| Database | PostgreSQL | MariaDB/PostgreSQL |
| Multi-tenancy | Built-in (ABP framework) | Site-per-tenant |
| Malaysia compliance | Native (LHDN, SST, EPF) | Via apps |
| API | REST + Swagger (auto-generated) | REST (Frappe) |
| Auth | OAuth 2.0 / OIDC (OpenIddict) | Session-based |
| Update | `docker compose pull && up -d` | `bench update` |
