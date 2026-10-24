# Production web image: builds the Vite app and serves it with Caddy. Build context: frontend/
FROM node:24-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Empty base URL = same-origin requests; Caddy proxies /api and /health to the API container.
ENV VITE_API_BASE_URL=""
RUN npm run build

FROM caddy:2-alpine
COPY Caddyfile /etc/caddy/Caddyfile
COPY --from=build /src/dist /srv
