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
# Modèle : au démarrage, l'image écrit /etc/nginx/conf.d/default.conf en remplaçant les variables ci-dessous
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
# Valeurs pour Docker Compose ; Azure les remplace (infra/main.bicep)
ENV API_UPSTREAM=http://api:8080 \
    STORAGE_ORIGIN=""
EXPOSE 8080