# ── Build stage ──────────────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies (layer-cached until package-lock changes)
COPY package*.json ./
RUN npm ci

# Build Angular app for Docker (apiUrl: '/api' — proxied by nginx)
COPY . .
RUN npm run build:docker

# ── Serve stage ───────────────────────────────────────────────────────────────
FROM nginx:alpine AS runtime
COPY --from=build /app/dist/ctrlvalue/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
