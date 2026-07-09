FROM node:22-alpine AS build

RUN corepack enable && corepack prepare pnpm@10.34.4 --activate

WORKDIR /app
COPY package.json pnpm-lock.yaml .npmrc /app/

RUN pnpm install --frozen-lockfile
COPY . /app

RUN pnpm build:prod

FROM nginx:alpine
LABEL org.opencontainers.image.source="https://github.com/myerp/myerp"
LABEL org.opencontainers.image.description="MyERP Web Frontend"

# Copy nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built Angular app
COPY --from=build /app/dist/MyERP/browser /usr/share/nginx/html

# Copy runtime entrypoint (injects API_URL at container start)
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

EXPOSE 80
ENTRYPOINT ["/docker-entrypoint.sh"]
