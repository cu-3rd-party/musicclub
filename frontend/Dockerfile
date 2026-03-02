ARG NODE_VERSION=22

FROM node:${NODE_VERSION}-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY tsconfig*.json vite.config.js index.html ./
COPY public ./public
COPY src ./src

ARG VITE_GRPC_HOST
ENV VITE_GRPC_HOST=$VITE_GRPC_HOST

RUN npm run build


FROM oven/bun:1.3-alpine AS runtime
WORKDIR /app

COPY --from=build /app/dist ./dist

EXPOSE 5173

CMD ["bunx", "serve", "-s", "dist", "-l", "5173"]
