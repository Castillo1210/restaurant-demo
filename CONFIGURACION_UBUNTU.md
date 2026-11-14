# Desarrollo en Ubuntu

Esta guía explica cómo trabajar en **desarrollo** con el Restaurante POS desde Ubuntu.
La **producción no cambia**: el local sigue con el servicio de Windows `RestaurantePOS`,
`INSTALAR.bat` / `ACTUALIZAR.bat` y la guía `INSTALACION_WINDOWS.md`. Desde Ubuntu solo se
programa, se prueba y se genera el paquete `.zip` para Windows.

> Regla de oro: la base de datos de desarrollo de este proyecto (`restaurant_demo`) vive en
> su **propio contenedor MySQL en el puerto 3307**. Nunca se usa el MySQL ni la base de Cara Negra.

---

## 1. Herramientas (una sola vez)

### .NET 10 SDK

Instalador oficial de Microsoft, queda en tu carpeta de usuario (`~/.dotnet`):

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
source ~/.bashrc
dotnet --version            # debe empezar con 10.
dotnet tool install --global dotnet-ef
```

### Node.js

Vite 8 pide Node 20.19 o superior. Con nvm:

```bash
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash
source ~/.bashrc
nvm install --lts
node --version
```

### Docker (para MySQL de desarrollo)

Si `docker --version` ya responde, salta este paso.

```bash
sudo apt update
sudo apt install -y docker.io
sudo usermod -aG docker $USER     # luego cierra sesión y vuelve a entrar
```

### PowerShell (para generar el paquete de Windows)

```bash
sudo snap install powershell --classic
pwsh --version
```

---

## 2. Base de datos de desarrollo

Un MySQL solo para este proyecto, escuchando únicamente en esta PC (`127.0.0.1:3307`) y con su
propio volumen de datos. Usa la misma versión mayor de MySQL que tiene la PC del restaurante
(abajo dice 8.0; cámbialo si allá es otra).

```bash
docker run -d --name restaurant-mysql --restart unless-stopped \
  -e MYSQL_ROOT_PASSWORD=Mina@200717 \
  -e MYSQL_DATABASE=cara_negra \
  -p 127.0.0.1:3307:3306 \
  -v restaurant_mysql_data:/var/lib/mysql \
  mysql:8.0
```

`TU_CLAVE_DEV` es una clave nueva, solo para tu PC (no la de producción ni la de Cara Negra).

Comandos útiles:

```bash
docker start restaurant-mysql      # si la PC se reinició y no levantó solo
docker logs restaurant-mysql       # ver errores
docker exec -it restaurant-mysql mysql -uroot -p restaurant_demo   # consola SQL
```

---

## 3. Secretos y variables (una sola vez)

### Secretos de la API (user-secrets, fuera del repositorio)

Se guardan en `~/.microsoft/usersecrets/`, nunca en `appsettings.json`:

```bash
cd src/CaraNegra.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1;Port=3307;Database=restaurant_demo;User=root;Password=Mina@200717;"
dotnet user-secrets set "JwtSettings:Secret" "$(openssl rand -base64 48)"

# Facturación: en el día a día, simulada (no sale a internet).
dotnet user-secrets set "FacturacionElectronica:Proveedor" "Simulado"

# Opcional: consulta de DNI/RUC con Decolecta.
dotnet user-secrets set "ConsultaDocumentos:Token" "sk_1802.FmwqpUfA9GSsik65Q51gw00fMjnZr456"

dotnet user-secrets list           # revisar
```

Cuando trabajes en algo de facturación electrónica, cambia a sandbox de APISUNAT
(nunca uses el token de producción en desarrollo):

```bash
dotnet user-secrets set "FacturacionElectronica:Proveedor" "ApiSunat"
dotnet user-secrets set "FacturacionElectronica:Token" "1073fDsOjDReiPsu8e22cM5xTYYwc1mInQMfzvAx2es63yqhZu6GVrokYTfGnR0PvpYuZCTlugKNV835kNiUDcFUluhUt8nsKBWsk4Tzh1BG346dEeiCItT98QRU"
```

Y al terminar, vuelve a `Simulado`.

### Variable para `dotnet ef` y para `publicar.ps1`

Las herramientas de migraciones **no leen user-secrets**: leen `RESTAURANT_DB_CONNECTION`
(ver `ApplicationDbContextFactory.cs`; a propósito ya no leen `CARANEGRA_DB_CONNECTION`).

```bash
echo 'export RESTAURANT_DB_CONNECTION="Server=127.0.0.1;Port=3307;Database=restaurant_demo;User=root;Password=Mina@200717;"' >> ~/.bashrc
source ~/.bashrc
```

### Frontend

```bash
cd cara-negra-frontend
cp .env.example .env.local         # VITE_API_URL=http://localhost:5014/api/v1
npm install
```

---

## 4. Crear las tablas (primera vez y después de cada migración nueva)

La API no aplica migraciones sola. Desde `src/CaraNegra.API`:

```bash
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Opcional, cargar la carta de La Casa del Cuy en la base de desarrollo (desde la raíz del proyecto):

```bash
sed '/^\s*USE\s/d' scripts/carta-la-casa-del-cuy.sql | docker exec -i restaurant-mysql mysql -uroot -pMina@200717 restaurant_demo
```

---

## 5. Trabajar día a día

Dos terminales:

```bash
# Terminal 1 — API en http://localhost:5014
cd src/CaraNegra.API
dotnet run --launch-profile http
```

```bash
# Terminal 2 — Frontend en http://localhost:5173
cd cara-negra-frontend
npm run dev
```

- **Primer arranque con base vacía**: la API crea un ADMIN con contraseña aleatoria y la
  muestra **una sola vez** en la terminal 1 (y en `src/CaraNegra.API/logs/`). Cópiala.
- **Probar con celulares en la misma red** (vista del mozo): `npm run dev -- --host` y abre
  `http://IP_DE_TU_PC:5173`. Si tienes el firewall activo: `sudo ufw allow 5173/tcp`.
  Vite reenvía `/api` y `/hubs` a la API (ver `vite.config.ts`).
- **Compilar y probar todo** desde la raíz:

  ```bash
  dotnet build CaraNegra.slnx
  dotnet test CaraNegra.slnx
  cd cara-negra-frontend && npm run build && npm run lint
  ```

### Impresión en desarrollo

- **Ticketera de cocina** (comandas de los mozos, por red al puerto 9100): está deshabilitada por
  defecto. Para probarla con una ticketera de red en tu misma red:

  ```bash
  cd src/CaraNegra.API
  dotnet user-secrets set "ImpresoraCocina:Habilitada" "true"
  dotnet user-secrets set "ImpresoraCocina:Ip" "IP_DE_LA_TICKETERA"
  ```

- **Boletas y comandas de ADMIN / CAJERO**: las imprime el navegador con `window.print()`, en la
  impresora predeterminada de Ubuntu (Configuración → Impresoras). Para probar sin papel, elige
  "Guardar como PDF" en el diálogo.

---

## 6. Generar el paquete para el restaurante (Windows)

Con el contenedor `restaurant-mysql` encendido (el script consulta la base para generar los
scripts de tablas) y `RESTAURANT_DB_CONNECTION` definida:

```bash
pwsh -File ./deploy/publicar.ps1
```

El resultado es `deploy/salida/RestaurantePOS-AAAA.MM.DD-HHMM.zip`: backend `win-x64`
autocontenido + frontend en `wwwroot` + scripts SQL + instaladores. Se lleva a la PC del
restaurante y se instala igual que siempre (`INSTALAR.bat` o `ACTUALIZAR.bat`, como administrador).

Si `publicar.ps1` falla en Ubuntu (por ejemplo, en una ruta escrita con `\`), no lo cambies a
mano: se ajusta para que funcione en los dos sistemas sin alterar lo que ya funciona en Windows.

---

## 7. Cuidados por trabajar en Linux

- **Mayúsculas y minúsculas**: Linux las distingue y Windows no. Un `import` que no coincide
  exactamente con el nombre del archivo funciona en Windows pero rompe `npm run build` en Ubuntu
  ("Could not resolve…"). Corrige el `import`, no el nombre del archivo.
- **Finales de línea (CRLF) y BOM**: los archivos del repositorio usan CRLF, y los `.ps1` además
  llevan BOM (PowerShell 5.1 de Windows lo necesita para leer bien las tildes). No conviertas
  archivos a LF ni quites el BOM. En git deja `core.autocrlf` en `false` (lo normal en Linux) y en
  el editor respeta el final de línea de cada archivo (en VS Code, `"files.eol": "auto"`).
- **Secretos**: solo en user-secrets (desarrollo) o en `appsettings.Production.json` del local.
  Nunca en el repositorio ni en este archivo.
- **No tocar Cara Negra**: ni su base de datos ni su MySQL. Todo lo de este proyecto apunta a
  `127.0.0.1:3307` / `restaurant_demo`.
