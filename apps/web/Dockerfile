# syntax=docker/dockerfile:1
# Build context: apps/web.
#   docker build -t vibezone-web apps/web

FROM node:24.22.0-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

FROM node:24.22.0-alpine AS build
WORKDIR /app
ENV NEXT_TELEMETRY_DISABLED=1
# /api/* rewrite target is fixed at build time (next.config.ts). Production normally routes
# /api at the reverse proxy instead (ADR-0002).
ARG API_INTERNAL_URL=http://api:8080
ENV API_INTERNAL_URL=$API_INTERNAL_URL
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:24.22.0-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0
# Standalone output already contains .next/static and public (scripts/prepare-standalone.mjs).
COPY --from=build --chown=node:node /app/.next/standalone ./
USER node
EXPOSE 3000
CMD ["node", "server.js"]
