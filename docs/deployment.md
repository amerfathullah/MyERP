# MyERP — Deployment Guide

## Environments

| Environment | Purpose | URL Pattern |
|-------------|---------|-------------|
| Development | Local development | `localhost:4200` / `localhost:5000` |
| Staging | Pre-production testing | `staging.myerp.example.com` |
| Production | Live system | `myerp.example.com` |

---

## Prerequisites

- Docker & Docker Compose v2+
- Domain name(s) pointing to your server
- SSL certificate (auto-provisioned via Let's Encrypt in production config)

---

## Local Development

### Quick Start

```bash
cd MyERP

# Start infrastructure (PostgreSQL + Redis)
docker compose up -d postgres redis

# Run database migrations
cd src/MyERP.DbMigrator && dotnet run && cd ../..

# Start API
cd src/MyERP.HttpApi.Host && dotnet run &

# Start Angular
cd angular && pnpm install && pnpm start
```

Or use the setup script:

```powershell
./setup-dev.ps1
```

### URLs

| Service | URL |
|---------|-----|
| Angular App | http://localhost:4200 |
| API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |

**Default credentials:** `admin` / `1q2w3E*`

---

## Docker Development (Full Stack)

```bash
docker compose up -d
```

This starts all services including the Angular app at `http://localhost:4200`.

---

## Production Deployment

### 1. Server Preparation

```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Create application directory
sudo mkdir -p /opt/myerp
sudo chown $USER:$USER /opt/myerp
cd /opt/myerp
```

### 2. Configuration

```bash
# Copy production compose file and environment template
cp docker-compose.prod.yml /opt/myerp/
cp .env.example /opt/myerp/.env

# Edit environment variables with ACTUAL production values
nano /opt/myerp/.env
```

**Critical variables to set:**

| Variable | Description |
|----------|-------------|
| `DB_PASSWORD` | Strong PostgreSQL password (32+ chars) |
| `REDIS_PASSWORD` | Strong Redis password |
| `API_URL` | Public API URL (e.g., `https://api.myerp.com`) |
| `APP_HOST` | Angular app domain (e.g., `myerp.com`) |
| `API_HOST` | API domain (e.g., `api.myerp.com`) |
| `ACME_EMAIL` | Email for Let's Encrypt certificates |
| `REGISTRY` | Container registry (e.g., `ghcr.io`) |
| `IMAGE_PREFIX` | Image prefix (e.g., `your-org/myerp`) |

### 3. Deploy

```bash
cd /opt/myerp

# Pull latest images
docker compose -f docker-compose.prod.yml pull

# Run database migrations
docker compose -f docker-compose.prod.yml run --rm migrator

# Start services
docker compose -f docker-compose.prod.yml up -d
```

### 4. Verify

```bash
# Check health
curl -sf https://api.myerp.com/health

# Check logs
docker compose -f docker-compose.prod.yml logs -f api

# Check all containers are running
docker compose -f docker-compose.prod.yml ps
```

---

## CI/CD Pipeline

The GitHub Actions workflow handles:

1. **On Push to `main`/`develop`**: Build + unit tests + lint
2. **On Tag `v*`**: Build Docker images → push to GHCR → deploy to staging
3. **Manual Dispatch**: Deploy to staging or production

### Required GitHub Secrets

| Secret | Purpose |
|--------|---------|
| `STAGING_HOST` | Staging server IP/hostname |
| `STAGING_USER` | SSH username for staging |
| `STAGING_SSH_KEY` | SSH private key for staging |
| `PROD_HOST` | Production server IP/hostname |
| `PROD_USER` | SSH username for production |
| `PROD_SSH_KEY` | SSH private key for production |

### Release Process

```bash
# Create a release tag
git tag v1.0.0
git push origin v1.0.0

# This triggers: build → test → push images → deploy staging
# Manual approval required for production deployment
```

---

## Database Management

### Migrations

Migrations are applied automatically by the `migrator` container during deployment. To apply manually:

```bash
docker compose -f docker-compose.prod.yml run --rm migrator
```

### Backup

```bash
# Manual backup
docker exec myerp-postgres pg_dump -U myerp MyERP | gzip > backup_$(date +%Y%m%d).sql.gz

# Automated daily backup (add to crontab)
0 2 * * * docker exec myerp-postgres pg_dump -U myerp MyERP | gzip > /opt/myerp/backups/daily_$(date +\%Y\%m\%d).sql.gz
```

### Restore

```bash
gunzip < backup_20260709.sql.gz | docker exec -i myerp-postgres psql -U myerp MyERP
```

---

## Monitoring

### Health Checks

- API: `GET /health` — returns 200 if healthy
- Angular: `GET /health` — Nginx returns 200
- PostgreSQL: `pg_isready` command
- Redis: `redis-cli ping`

### Logs

```bash
# All services
docker compose -f docker-compose.prod.yml logs -f

# Specific service
docker compose -f docker-compose.prod.yml logs -f api

# Last 100 lines
docker compose -f docker-compose.prod.yml logs --tail 100 api
```

### Resource Monitoring

```bash
docker stats
```

---

## Scaling

### Horizontal Scaling (API)

```yaml
# In docker-compose.prod.yml, add replicas:
api:
  deploy:
    replicas: 3
```

Traefik automatically load-balances across replicas.

### Database Scaling

For high-traffic deployments:
- Enable PostgreSQL read replicas
- Configure connection pooling (PgBouncer)
- Separate Redis instances for cache vs distributed events

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| API won't start | Check DB connection string, ensure PostgreSQL is healthy |
| Angular shows blank page | Verify `dynamic-env.json` has correct API URL |
| 502 Bad Gateway | API container crashed — check logs |
| Permission denied | Ensure docker volume permissions are correct |
| SSL certificate errors | Verify domain DNS, check Traefik ACME logs |
| Migration fails | Check migration logs, ensure DB user has create/alter permissions |
