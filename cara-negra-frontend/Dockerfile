# ============================================================
#  Dockerfile — cara-negra-frontend (React + Vite)
#  Build de contexto: se debe ejecutar desde esta carpeta, ej.:
#    docker build --build-arg VITE_API_URL=https://api.cara-negra.pe/api/v1 -t caranegra-frontend .
#  (o con `docker compose build`, que ya pasa el build-arg — ver
#  docker-compose.yml en la raíz del repo).
# ============================================================

# ---- Etapa 1: build ----
FROM node:22-alpine AS build
WORKDIR /app

# La URL del backend queda "horneada" en el bundle de JS en tiempo de build
# (Vite reemplaza import.meta.env.VITE_API_URL de forma estática): por eso
# es un ARG de build y no una variable de entorno de runtime del contenedor.
ARG VITE_API_URL=http://localhost:5014/api/v1
ENV VITE_API_URL=$VITE_API_URL

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

# ---- Etapa 2: runtime (nginx sirviendo archivos estáticos) ----
FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080
