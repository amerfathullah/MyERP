#!/bin/bash
# =============================================================================
# MyERP — Quick Install Script
# =============================================================================
# Usage:
#   curl -sL https://raw.githubusercontent.com/myerp/myerp/main/MyERP/deploy/install.sh | bash
#
# Or download and run:
#   wget -O install.sh https://raw.githubusercontent.com/myerp/myerp/main/MyERP/deploy/install.sh
#   chmod +x install.sh && ./install.sh
# =============================================================================

set -e

INSTALL_DIR="${MYERP_INSTALL_DIR:-./myerp}"
REPO_URL="https://raw.githubusercontent.com/myerp/myerp/main/MyERP/deploy"

echo ""
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║                  MyERP Self-Hosted Install                   ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Full-featured ERP system with:                             ║"
echo "║  • Accounting, Sales, Purchasing, Inventory                 ║"
echo "║  • HR & Payroll (Malaysia: EPF, SOCSO, EIS, PCB)           ║"
echo "║  • LHDN e-Invoice (MyInvois) integration                   ║"
echo "║  • CRM, Projects, Manufacturing, Fixed Assets              ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""

# Check prerequisites
echo "Checking prerequisites..."

if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first:"
    echo "   https://docs.docker.com/get-docker/"
    exit 1
fi

if ! docker compose version &> /dev/null 2>&1; then
    echo "❌ Docker Compose V2 is not available. Please update Docker."
    exit 1
fi

echo "✅ Docker & Docker Compose found"
echo ""

# Create install directory
echo "Installing to: ${INSTALL_DIR}"
mkdir -p "${INSTALL_DIR}"
cd "${INSTALL_DIR}"

# Download docker-compose.yml
echo "Downloading docker-compose.yml..."
curl -sL "${REPO_URL}/docker-compose.yml" -o docker-compose.yml

# Download .env.example
echo "Downloading .env.example..."
curl -sL "${REPO_URL}/.env.example" -o .env.example

# Generate .env if it doesn't exist
if [ ! -f .env ]; then
    echo "Generating .env with secure random password..."
    DB_PASS=$(openssl rand -base64 24 | tr -d '/+=' | head -c 32)
    cp .env.example .env
    sed -i "s/change_me_to_a_strong_password/${DB_PASS}/" .env
    echo "✅ .env created with auto-generated database password"
else
    echo "⚠️  .env already exists, skipping (keeping your existing config)"
fi

echo ""
echo "Pulling Docker images..."
docker compose pull

echo ""
echo "Starting MyERP..."
docker compose up -d

echo ""
echo "Waiting for services to be ready..."
sleep 10

# Wait for API health check
MAX_WAIT=60
ELAPSED=0
while [ $ELAPSED -lt $MAX_WAIT ]; do
    if docker compose exec -T api curl -sf http://localhost:5000/health > /dev/null 2>&1; then
        break
    fi
    sleep 3
    ELAPSED=$((ELAPSED + 3))
    echo "  Waiting for API... (${ELAPSED}s)"
done

if [ $ELAPSED -ge $MAX_WAIT ]; then
    echo "⚠️  API took longer than expected to start. Check logs with:"
    echo "   cd ${INSTALL_DIR} && docker compose logs api"
else
    echo "✅ API is healthy"
fi

echo ""
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║                    🎉 MyERP is running!                     ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║                                                             ║"
echo "║  Web App:  http://localhost                                 ║"
echo "║  API:      http://localhost:5000                            ║"
echo "║  Swagger:  http://localhost:5000/swagger                    ║"
echo "║                                                             ║"
echo "║  Login:    admin / 1q2w3E*                                  ║"
echo "║                                                             ║"
echo "║  ⚠️  Change the default password after first login!         ║"
echo "║                                                             ║"
echo "╠══════════════════════════════════════════════════════════════╣"
echo "║  Useful Commands:                                           ║"
echo "║  • View logs:     docker compose logs -f                    ║"
echo "║  • Stop:          docker compose down                       ║"
echo "║  • Update:        docker compose pull && docker compose up -d║"
echo "║  • Backup DB:     docker compose exec db pg_dump -U myerp MyERP║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""
