# Despliegue de Cara Negra en otra máquina

> ⚠️ Antes de seguir esta guía, resuelve el incidente de seguridad descrito al final de este
> documento (sección "Aviso de seguridad") si aún no lo hiciste — hay credenciales reales
> expuestas en el repositorio público de GitHub.

Esta guía cubre cómo llevar el sistema completo (backend .NET 10 + frontend React + MySQL) a
una máquina nueva usando Docker, tanto en Windows como en Linux. Todo el despliegue se apoya en
los archivos ya preparados en la Fase 6: `docker-compose.yml`, los `Dockerfile` de
`src/CaraNegra.API` y `cara-negra-frontend`, y `.env.example`.

## 1. Requisitos en la máquina destino

### Windows
- [Git for Windows](https://git-scm.com/download/win)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (con el backend WSL2 habilitado)

### Linux (Ubuntu/Debian, típico de un VPS)
- `git`
- Docker Engine + el plugin `docker compose` (`sudo apt install docker.io docker-compose-plugin`, o seguir la [guía oficial de instalación](https://docs.docker.com/engine/install/))
- El usuario debe estar en el grupo `docker` (`sudo usermod -aG docker $USER`, luego cerrar sesión y volver a entrar) para no tener que usar `sudo` en cada comando

No hace falta instalar el SDK de .NET ni Node.js en la máquina destino — todo corre dentro de
los contenedores Docker.

## 2. Clonar el repositorio

**Windows (PowerShell o CMD):**
```powershell
cd C:\
git clone https://github.com/Castillo1210/cara_negra.git
cd cara_negra
```

**Linux (bash):**
```bash
cd ~
git clone https://github.com/Castillo1210/cara_negra.git
cd cara_negra
```

## 3. Configurar variables de entorno

Copia la plantilla y edítala con valores reales (nunca reutilices las credenciales que quedaron
expuestas en GitHub — genera contraseñas y un secreto JWT nuevos para cada entorno).

**Windows (PowerShell):**
```powershell
Copy-Item .env.example .env
notepad .env
```

**Linux:**
```bash
cp .env.example .env
nano .env
```

Como mínimo, completa:
- `MYSQL_ROOT_PASSWORD` y `MYSQL_PASSWORD`: contraseñas nuevas, distintas a las de tu máquina de desarrollo.
- `JWT_SECRET`: una cadena aleatoria larga. Para generar una:
  - Windows PowerShell: `[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))`
  - Linux: `openssl rand -base64 48`
- `FRONTEND_URL`: la URL pública desde donde se accederá al frontend (para CORS). Si es solo para pruebas en red local, puede quedar `http://localhost:8081` o `http://IP_DE_LA_MAQUINA:8081`.
- `VITE_API_URL`: la URL pública desde la que el **navegador** del cliente alcanza la API (no el nombre interno de Docker). Ej.: `http://IP_DE_LA_MAQUINA:8080/api/v1`.
- `IMPRESORA_IP` (opcional): la IP de la impresora térmica de cocina en la red de esta ubicación, si es distinta a la de desarrollo.

## 4. Levantar los contenedores

```bash
docker compose up -d --build
```

Esto construye las imágenes del backend y frontend, y levanta MySQL, la API (puerto 8080) y el
frontend (puerto 8081). La primera vez tardará varios minutos (descarga imágenes base y
restaura paquetes NuGet/npm dentro del build).

Verifica que los tres contenedores estén corriendo:
```bash
docker compose ps
```

## 5. Aplicar el esquema de base de datos (solo la primera vez)

La base de datos se levanta vacía — hay que crear las tablas. Hay dos caminos, según si la
máquina destino tiene o no el SDK de .NET instalado.

### Opción A — Sin SDK de .NET en la máquina destino (recomendado para un servidor limpio)

En tu máquina de desarrollo (finan13, que sí tiene el SDK), genera un script SQL portátil a
partir de las migraciones y súbelo al repositorio **antes** de desplegar en la otra máquina:

```powershell
cd D:\cara_negra\src\CaraNegra.API
dotnet ef migrations script --idempotent -o ..\..\db\schema.sql --project ..\CaraNegra.Infrastructure --startup-project .
```

Luego agrégalo al commit que subas a GitHub (ver sección "Comandos para subir a GitHub" más
abajo) — con esto, `db/schema.sql` queda versionado y disponible al clonar en cualquier máquina.

En la máquina destino, con los contenedores ya levantados (paso 4), aplica el script:

**Linux:**
```bash
cat db/schema.sql | docker compose exec -T db mysql -uroot -p"TU_MYSQL_ROOT_PASSWORD" caranegra
```

**Windows (PowerShell):**
```powershell
Get-Content db\schema.sql -Raw | docker compose exec -T db mysql -uroot -p"TU_MYSQL_ROOT_PASSWORD" caranegra
```

(Reemplaza `TU_MYSQL_ROOT_PASSWORD` por el valor real que pusiste en `.env`, y `caranegra` por
el valor de `MYSQL_DATABASE` si lo cambiaste.)

### Opción B — Si la máquina destino sí tiene el SDK de .NET

```bash
cd src/CaraNegra.API
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project . --connection "Server=localhost;Port=3306;Database=caranegra;User=caranegra;Password=TU_PASSWORD;"
```

(El puerto 3306 de MySQL queda expuesto por `docker-compose.yml`, así que esto funciona aunque
la base de datos esté dentro de un contenedor.)

## 6. Verificar que todo funciona

- Backend: abre `http://localhost:8080/health` (o la IP de la máquina) — debe responder `Healthy`.
- Frontend: abre `http://localhost:8081` — debe cargar la pantalla de login.
- Si algo falla, revisa los logs:
  ```bash
  docker compose logs -f api
  docker compose logs -f frontend
  docker compose logs -f db
  ```

La primera vez que arranca la API sin ningún usuario ADMIN en la base, se crea uno automático
con contraseña aleatoria — revisa el log de `api` (`docker compose logs api`) justo después del
primer arranque para capturarla, se muestra una sola vez.

## 7. Notas para un despliegue real (no solo pruebas)

- **HTTPS**: pon un reverse proxy (Caddy o nginx + Certbot) delante de los puertos 8080/8081
  con un dominio propio — este `docker-compose.yml` sirve todo en HTTP plano, pensado para redes
  internas o pruebas.
- **Firewall**: si es un servidor en la nube, abre solo los puertos que necesites expuestos al
  público (normalmente 80/443 si usas reverse proxy) y cierra el 3306 (MySQL) al público —
  déjalo accesible solo desde la propia máquina o tu VPN.
- **Backups**: los datos de MySQL viven en el volumen Docker `caranegra_db_data`. Programa un
  `mysqldump` periódico, por ejemplo:
  ```bash
  docker compose exec db mysqldump -uroot -p"TU_PASSWORD" caranegra > backup_$(date +%Y%m%d).sql
  ```
- **Actualizar a una nueva versión**: `git pull` seguido de `docker compose up -d --build`.
- **Impresora de cocina**: si esta ubicación tiene una impresora térmica distinta, ajusta
  `IMPRESORA_IP` en `.env` y reinicia solo la API: `docker compose restart api`.

---

## Comandos para subir a GitHub (desde tu máquina, finan13)

Antes de subir, resuelve el aviso de seguridad de abajo. Una vez resuelto, para subir todo el
trabajo de las Fases 0–6:

```powershell
cd D:\cara_negra
git add -A
git status
git commit -m "Sistema completo Cara Negra: Fases 0-6 (mozo, caja, carta, admin, usuarios, inventario, ticketera, PWA, despliegue)"
git push origin main
```

`git status` antes del commit es solo para revisar qué se va a subir — confirma que no aparezca
ningún archivo con secretos reales (contraseñas, `.env`) en la lista.

---

## Aviso de seguridad — leer antes de subir nada más

Al revisar el repositorio antes de escribir esta guía, encontré que el commit
`27b7dfe` ("initial commit"), ya subido a `https://github.com/Castillo1210/cara_negra`
**(repositorio público)**, contiene en texto plano en `src/CaraNegra.API/appsettings.json`:

- La contraseña real del usuario `root` de tu MySQL.
- El secreto real usado para firmar los tokens JWT.

Esto quedó ahí antes de que se corrigiera el proyecto para nunca guardar secretos reales en ese
archivo (la versión actual en tu disco ya tiene esos campos vacíos, tal como debe ser — pero el
commit viejo en GitHub todavía los expone públicamente).

**Acciones que debes tomar ahora, en este orden:**

1. **Cambia la contraseña de root de MySQL de inmediato.** Conéctate con tu cliente de MySQL
   habitual (Workbench, `mysql -u root -p`, etc.) y ejecuta:
   ```sql
   ALTER USER 'root'@'localhost' IDENTIFIED BY 'UNA_CONTRASEÑA_NUEVA_Y_DISTINTA';
   FLUSH PRIVILEGES;
   ```
   Luego actualiza esa contraseña en tus secretos locales:
   ```powershell
   cd D:\cara_negra\src\CaraNegra.API
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=cara_negra;User=root;Password=UNA_CONTRASEÑA_NUEVA_Y_DISTINTA;"
   ```

2. **Genera un secreto JWT nuevo** (invalida todas las sesiones activas — está bien, todos
   simplemente vuelven a iniciar sesión). Aquí tienes uno recién generado, listo para usar:
   ```
   PfLLajVdK41sHREZdwPxyuzqtmNAqaq5zui72mDAEAfblz-Y4IeymxJjMzJVQLWb
   ```
   Configúralo:
   ```powershell
   dotnet user-secrets set "JwtSettings:Secret" "PfLLajVdK41sHREZdwPxyuzqtmNAqaq5zui72mDAEAfblz-Y4IeymxJjMzJVQLWb"
   ```

3. **Elimina el secreto del historial de GitHub.** Como el repositorio solo tiene un commit y
   0 forks/stars, la forma más segura y simple es borrar el repositorio y crearlo de nuevo:

   a. En GitHub, ve a `https://github.com/Castillo1210/cara_negra/settings`, baja hasta
      "Danger Zone" → "Delete this repository" y sigue la confirmación.

   b. Crea un repositorio nuevo con el mismo nombre en <https://github.com/new> — **no** lo
      inicialices con README, .gitignore ni licencia (para que el push no choque).

   c. En tu máquina, reinicia el historial local y sube todo de nuevo:
      ```powershell
      cd D:\cara_negra
      Remove-Item -Recurse -Force .git
      git init
      git branch -M main
      git remote add origin https://github.com/Castillo1210/cara_negra.git
      git add -A
      git commit -m "Sistema completo Cara Negra: Fases 0-6"
      git push -u origin main
      ```

   **Alternativa más rápida, sin borrar el repositorio** (algo menos segura: GitHub pudo haber
   cacheado brevemente el contenido del commit viejo, aunque con 0 forks es muy poco probable
   que alguien lo haya copiado):
   ```powershell
   cd D:\cara_negra
   git add -A
   git commit --amend -m "Sistema completo Cara Negra: Fases 0-6"
   git push --force origin main
   ```

Una vez hecho esto, sigue con la sección "Comandos para subir a GitHub" de más arriba para las
próximas veces que subas cambios normales (ya no hará falta `--force` ni tocar el historial).
