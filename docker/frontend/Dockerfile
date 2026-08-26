# syntax=docker/dockerfile:1.7

FROM node:22-alpine AS base

WORKDIR /app

RUN corepack enable

FROM base AS prod-deps

# Copy only frontend package files
COPY src/Web/ClientApp/package.json src/Web/ClientApp/pnpm-lock.yaml ./
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
	pnpm install --frozen-lockfile --prod

FROM base AS build-deps

# Copy only frontend package files
COPY src/Web/ClientApp/package.json src/Web/ClientApp/pnpm-lock.yaml ./
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
	pnpm install --frozen-lockfile

FROM build-deps AS builder

# Copy only frontend source
COPY src/Web/ClientApp/ ./

RUN pnpm run build

FROM node:22-alpine

WORKDIR /app

COPY --from=prod-deps /app/node_modules ./node_modules
COPY --from=builder /app/build ./build
COPY src/Web/ClientApp/package.json ./
COPY src/Web/ClientApp/static ./static

ENV HOST=0.0.0.0
ENV PORT=5173
ENV NODE_ENV=production

EXPOSE 5173

# Health check
HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:5173 || exit 1

CMD ["node", "build/index.js"]
