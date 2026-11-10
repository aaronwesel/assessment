# React/Vite SPA → nginx static host for Compose / CloudFront-shaped local parity.
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Bake public API bases at build time (Compose build.args / CI).
ARG VITE_API_BASE_URL=http://localhost:8000
ARG VITE_CRAWL_API_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_CRAWL_API_URL=$VITE_CRAWL_API_URL

RUN npm run build

FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 3000
HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
  CMD wget -qO- http://127.0.0.1:3000/ || exit 1
CMD ["nginx", "-g", "daemon off;"]
