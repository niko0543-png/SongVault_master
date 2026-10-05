# syntax=docker/dockerfile:1

# ---------- Étape 1 : build Vite (Node, jamais livré) ----------
FROM node:24-alpine AS build
WORKDIR /app

# Dépendances d'abord (cache) ; npm ci = installation EXACTE selon package-lock.json
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# ---------- Étape finale : nginx non privilégié ----------
FROM nginxinc/nginx-unprivileged:stable-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080