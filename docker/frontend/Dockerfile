# syntax=docker/dockerfile:1.7

FROM node:22-alpine AS base

WORKDIR /app

RUN corepack enable

FROM base AS prod-deps

# Copy only frontend package files
COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
	pnpm install --frozen-lockfile --prod

FROM base AS build-deps

# Copy only frontend package files
COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
	pnpm install --frozen-lockfile

FROM build-deps AS builder

# Copy only frontend source
COPY frontend/ ./

RUN pnpm run build

FROM nginx:alpine

# Copy custom nginx config
COPY docker/frontend/nginx.conf /etc/nginx/conf.d/default.conf

# Copy built frontend
COPY --from=builder /app/dist/frontend/browser /usr/share/nginx/html

# Health check
HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:80 || exit 1

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
