# syntax=docker/dockerfile:1.7

FROM node:22-alpine AS base

WORKDIR /app

RUN corepack enable

COPY package.json pnpm-lock.yaml ./
RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
	pnpm fetch --frozen-lockfile

FROM base AS prod-deps

RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
	pnpm install --frozen-lockfile --prod --offline

FROM base AS build-deps

RUN --mount=type=cache,id=pnpm-store,target=/pnpm/store \
	pnpm install --frozen-lockfile --offline

FROM build-deps AS builder

COPY . .
RUN pnpm run build

FROM node:22-alpine

WORKDIR /app

COPY --from=prod-deps /app/node_modules ./node_modules
COPY --from=builder /app/build ./build
COPY package.json ./package.json

ENV HOST=0.0.0.0
ENV PORT=5173

EXPOSE 5173

CMD ["node", "build/index.js"]
