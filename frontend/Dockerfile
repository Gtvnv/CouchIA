# syntax=docker/dockerfile:1
# Contexto de build: a pasta frontend/ (veja docker-compose.yml na raiz).

# ---- build ----
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# "vite build" direto: o typecheck já roda nos testes do projeto.
RUN npx vite build

# ---- runtime ----
FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
