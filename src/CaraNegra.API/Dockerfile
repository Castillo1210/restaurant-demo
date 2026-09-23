# ============================================================
#  Dockerfile — CaraNegra.API (backend .NET 10)
#  Build de contexto: se debe ejecutar desde la raíz del repo, ej.:
#    docker build -f src/CaraNegra.API/Dockerfile -t caranegra-api .
#  (o simplemente con `docker compose build`, que ya usa la raíz como
#  contexto — ver docker-compose.yml).
# ============================================================

# ---- Etapa 1: build/publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiamos solo los .csproj primero para aprovechar el cache de capas de
# Docker: mientras no cambien las dependencias, "dotnet restore" no se
# vuelve a ejecutar aunque cambie el código fuente.
COPY src/CaraNegra.API/CaraNegra.API.csproj src/CaraNegra.API/
COPY src/CaraNegra.Application/CaraNegra.Application.csproj src/CaraNegra.Application/
COPY src/CaraNegra.Domain/CaraNegra.Domain.csproj src/CaraNegra.Domain/
COPY src/CaraNegra.Infrastructure/CaraNegra.Infrastructure.csproj src/CaraNegra.Infrastructure/
RUN dotnet restore src/CaraNegra.API/CaraNegra.API.csproj

# Ahora sí copiamos el resto del código y publicamos en Release.
COPY src/CaraNegra.API/ src/CaraNegra.API/
COPY src/CaraNegra.Application/ src/CaraNegra.Application/
COPY src/CaraNegra.Domain/ src/CaraNegra.Domain/
COPY src/CaraNegra.Infrastructure/ src/CaraNegra.Infrastructure/
RUN dotnet publish src/CaraNegra.API/CaraNegra.API.csproj -c Release -o /app/publish --no-restore

# ---- Etapa 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Usuario sin privilegios (la imagen base ya trae "app"/uid 64198) en vez de root.
USER app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CaraNegra.API.dll"]
