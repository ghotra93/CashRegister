# CashRegister UI: builds the Vite app, then serves it with nginx, which also forwards /api and
# /health to the API container so the browser talks to a single origin.
#   docker build -t cashregister-web web

FROM node:24-alpine AS build
WORKDIR /web

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist /usr/share/nginx/html
EXPOSE 80
