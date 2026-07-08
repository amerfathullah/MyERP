FROM node:22-alpine AS build

RUN corepack enable && corepack prepare pnpm@10.34.4 --activate

WORKDIR /app
COPY package.json pnpm-lock.yaml /app/

RUN pnpm install --frozen-lockfile
COPY . /app

RUN pnpm build:prod

FROM nginx:alpine
COPY dynamic-env.json /usr/share/nginx/html
COPY nginx.conf  /etc/nginx/conf.d/default.conf

COPY --from=build /app/dist/MyERP/browser /usr/share/nginx/html
