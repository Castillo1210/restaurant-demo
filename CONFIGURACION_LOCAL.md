# Configuración local después de la Fase 0

Este archivo explica los pasos que hay que correr **una sola vez en tu máquina** después de
los cambios de seguridad de la Fase 0. No pude ejecutar estos comandos yo mismo porque el
entorno donde trabajé no tiene acceso a internet para instalar el SDK de .NET ni para
restaurar paquetes NuGet.

## 1. Configurar los secretos (ya no están en appsettings.json)

Desde la carpeta `src/CaraNegra.API`, ejecuta:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=cara_negra;User=root;Password=Mina@200717;"
dotnet user-secrets set "JwtSettings:Secret" "K7mP9xR2dL8vN4qT6cY1hJ5wB3sF0gZe"
```

Esto guarda los valores fuera del repositorio (en tu perfil de usuario), así que nunca se
vuelven a subir a git. En producción, usa variables de entorno en su lugar:
`ConnectionStrings__DefaultConnection` y `JwtSettings__Secret` (doble guion bajo).

Si por algún motivo la contraseña real de MySQL quedó expuesta en el historial de git
(`Mina@200717`, que estaba hardcodeada antes de esta Fase 0), cámbiala en MySQL también,
no solo en la configuración.

## 2. Configurar la variable de entorno para las migraciones de EF Core

`ApplicationDbContextFactory` (usado por la herramienta `dotnet ef`) ahora lee la cadena de
conexión de una variable de entorno en vez de tenerla escrita en el código:

**Windows (PowerShell):**
```powershell
setx CARANEGRA_DB_CONNECTION "Server=localhost;Port=3306;Database=cara_negra;User=root;Password=Mina@200717;"
```
(Cierra y vuelve a abrir la terminal/Visual Studio después de `setx` para que tome efecto.)

**Linux/macOS:**
```bash
export CARANEGRA_DB_CONNECTION="Server=localhost;Port=3306;Database=cara_negra;User=root;Password=TU_PASSWORD_REAL;"
```

## 3. Generar y aplicar la migración inicial

El proyecto nunca tuvo migraciones de Entity Framework versionadas. Ahora que el modelo
tiene los campos nuevos de auditoría de pagos e índices únicos, genera la migración inicial
completa:

```bash
cd src/CaraNegra.API
dotnet ef migrations add InitialCreate --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Revisa el archivo de migración generado antes de aplicarlo (por si tu base de datos ya tiene
datos y necesitas ajustar algo manualmente, por ejemplo si ya existen números de mesa o
nombres de usuario duplicados, ya que los nuevos índices únicos rechazarán el `database update`
hasta que se resuelvan esos duplicados).

## 4. Verificar que compila

```bash
dotnet build CaraNegra.slnx
dotnet test CaraNegra.slnx
```

Los proyectos `CaraNegra.Tests` y `CaraNegra.IntegrationTests` ahora están incluidos en la
solución (antes no lo estaban, así que un pipeline de CI basado en la solución podía no
ejecutarlos nunca).

Si `dotnet build` marca algún error, compárteme el mensaje completo y lo corrijo de inmediato
— no pude compilar este código en el entorno donde lo escribí.

## 5. Frontend

No cambié dependencias del frontend en esta fase, solo eliminé archivos huérfanos
(`Menu.jsx`, `CartSheet.jsx`, `App.css`) y corregí la nomenclatura de las llaves de `localStorage`
del carrito (`elRobleCart`/`elRobleTable` → `caraNegraCart`/`caraNegraTable`). Si tenías un
carrito guardado en el navegador con las llaves viejas, se perderá al actualizar (es
esperable, es solo un carrito de prueba en memoria local).

## 6. Fase 1 — Módulo Mozo (backend real + frontend conectado)

### Backend

- Se agregó `PedidosController` (no existía), `UpdatePedidoEstadoCommand`,
  `AgregarDetallePedidoCommand`, `EliminarDetallePedidoCommand`, `GetAllPedidosQuery` y
  `GetPedidoByIdQuery`. No se agregó ninguna columna nueva a la base de datos en esta fase,
  así que **no hace falta generar una migración nueva** todavía.
- Se conectó `IPedidosHubService` (SignalR) a `CreatePedidoCommand`, `CreatePagoCommand`,
  `AnularPagoCommand` y al nuevo `UpdatePedidoEstadoCommand`, para que el mapa de mesas y
  las pantallas de mozo/caja se actualicen en tiempo real sin recargar.
- **Importante**: se agregó un `JsonStringEnumConverter` global en `Program.cs`. Esto hace
  que todos los enums (`EstadoPedido`, `EstadoMesa`, `EstadoDetallePedido`, etc.) se
  serialicen como texto (`"Pendiente"`, `"Ocupada"`) en vez de números (`0`, `1`...). Si ya
  tenías algún cliente HTTP de prueba (Postman, `.http`) que mandaba números para estos
  campos, ahora debe mandar el nombre del enum como string.
- Se corrigió la autenticación de SignalR: el JWT del navegador ahora también se acepta
  como query string `access_token` en la conexión al hub (`/hubs/pedidos`), porque el
  navegador no permite mandar el header `Authorization` en el handshake de WebSocket.
- Se permitió crear un pedido nuevo en una mesa `Reservada` además de `Disponible` (antes
  solo `Disponible`), para cubrir el caso de "el cliente de la reserva llega y se sienta".

### Frontend

- El frontend del mozo (`cara-negra-frontend`) ahora habla con el backend real: login por
  usuario/contraseña (ya no PIN), mapa de mesas, toma de pedidos y envío a cocina, todo vía
  HTTP + JWT, más SignalR para actualizaciones en vivo.
- Copia `cara-negra-frontend/.env.example` a `cara-negra-frontend/.env.local` y ajusta
  `VITE_API_URL` según el perfil de `launchSettings.json` que uses (`http://localhost:5014/api/v1`
  para el perfil `http`, o `https://localhost:7108/api/v1` para `https`).
- El catálogo (categorías/productos) se carga desde `/categorias` y `/productos`. Como pediste,
  **no se cargó ningún producto de ejemplo** — hasta que no crees categorías y productos desde
  el panel de administrador (Fase 3), la pantalla de toma de pedido se verá vacía. Esto es
  esperado, no un error.
- Para poder probar el flujo de punta a punta ahora mismo (antes de que exista el panel de
  Fase 3), necesitas crear manualmente al menos una mesa, una categoría y un producto. Puedes
  hacerlo con `POST /api/v1/mesas`, `POST /api/v1/categorias` y `POST /api/v1/productos` desde
  Postman/Scalar usando un usuario ADMIN.

### Primer ADMIN (siembra automática)

Registrar un usuario nuevo requiere estar logueado como ADMIN, pero una base de datos recién
creada no tiene ningún usuario — es un problema de "huevo y gallina". Para resolverlo, agregué
`AdminBootstrapSeeder`: la primera vez que arrancas el backend (`dotnet run`) y no hay ningún
usuario con rol ADMIN, se crea automáticamente uno con usuario `admin` y una contraseña
aleatoria de 14 caracteres, que se imprime **una sola vez** en la consola/log, como una
advertencia bien visible (`=== Se creó un usuario ADMIN inicial... ===`). Cópiala de ahí,
inicia sesión, y cambia la contraseña desde tu perfil cuanto antes. La próxima vez que
arranques el sistema ya no se mostrará ningún mensaje, porque ya existirá al menos un ADMIN.

Este seeder también crea los roles MOZO y CAJERO si no existen, para que puedas dar de alta
personal desde `/auth/register` sin tener que crear los roles a mano primero.

No se ejecuta en el entorno "Testing" (las pruebas de integración siembran sus propios
usuarios de prueba).
- Sigue pendiente correr `npm install` en `cara-negra-frontend` si no lo habías hecho (no
  cambié dependencias, pero por si acaso — `axios` y `@microsoft/signalr` ya estaban en
  `package.json` desde antes, solo que no se usaban).

## 7. Fase 2 — Módulo Caja/POS (web)

### Backend

- **Nueva migración necesaria**: se agregó la columna `EstaActivo` (bool, default `true`) a
  `MetodoPago`, para poder desactivar un método de pago (p.ej. dejar de aceptar Yape) sin
  borrarlo — los pagos históricos siguen apuntando a él. Genera y aplica la migración:
  ```bash
  cd src/CaraNegra.API
  dotnet ef migrations add AgregarEstaActivoAMetodoPago --project ../CaraNegra.Infrastructure --startup-project .
  dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
  ```
- Se agregó `MetodosPagoController` (no existía) con su CRUD completo: `CreateMetodoPagoCommand`,
  `UpdateMetodoPagoCommand`, `DeleteMetodoPagoCommand` (borrado lógico), `GetAllMetodosPagoQuery`,
  `GetMetodoPagoByIdQuery`. La lectura (`GET /metodos-pago`) está disponible para CAJERO y ADMIN;
  crear/editar/desactivar un método queda reservado a ADMIN, ya que afecta a todo el sistema.
- El resto del backend de cobros (`PagosController`, `CreatePagoCommand` con soporte de pago
  mixto/parcial, `AnularPagoCommand` con auditoría de anulación) **ya existía de fases
  anteriores** — no hizo falta tocarlo, solo faltaba el catálogo de métodos de pago.
- Se agregó `MetodoPagoSeeder`: al arrancar (fuera del entorno "Testing"), siembra
  automáticamente los métodos "Efectivo", "Tarjeta", "Yape", "Plin" y "Transferencia" si no
  existen todavía, para que caja sea utilizable desde el primer arranque sin depender del
  panel de administración (Fase 3/5, que aún no existen). Es idempotente: no duplica ni pisa
  los que el ADMIN ya haya editado o desactivado.
- El validador de `CreatePagoDto` ahora exige que el método de pago exista **y esté activo**
  (antes solo exigía que existiera).

### Frontend

- Nueva página `/caja` (`pages/Caja.jsx`), protegida por rol: solo CAJERO y ADMIN pueden
  entrar (MOZO es redirigido de vuelta al mapa de mesas si intenta acceder). Para esto,
  `RequireAuth` ahora acepta un prop opcional `roles={['CAJERO', 'ADMIN']}`.
- Desde el Dashboard (mapa de mesas), los usuarios CAJERO/ADMIN ven un botón adicional junto
  a su avatar para ir directo a caja.
- La pantalla de caja lista los pedidos en estado "Listo" (comida entregada por cocina,
  pendiente de cobro). Al tocar un pedido se abre un panel con el detalle, el saldo pendiente,
  el selector de método de pago (cargado desde `/metodos-pago`) y el monto a cobrar — soporta
  pagos parciales/mixtos (p.ej. mitad efectivo, mitad Yape) porque el backend ya lo permitía.
  También se puede anular un pago ya registrado (pide un motivo, igual que exige el backend).
- Al completarse el pago total del pedido, se muestra un comprobante interno simple e
  imprimible (`components/ReciboPedido.jsx`, botón "Imprimir" que usa `window.print()` con
  estilos `@media print` en `index.css` para que solo se imprima el ticket). **No es un
  comprobante electrónico SUNAT** — es solo un ticket de control interno, tal como se definió
  para esta primera etapa.
- Todo el panel de caja se actualiza en tiempo real vía SignalR (nuevos pedidos, cambios de
  estado, pagos recibidos/anulados), igual que el mapa de mesas del mozo.

## 8. Fase 3 — Administración de carta/menú

### Backend

- El CRUD de `CategoriasController` y `ProductosController` **ya existía** de una fase
  anterior (crear/editar/desactivar categorías y productos). No hizo falta crear nada nuevo
  ahí, solo dos ajustes:
  - **Restricción de roles**: crear/editar/desactivar categorías o productos ahora requiere
    rol ADMIN (antes cualquier usuario autenticado —incluido MOZO— podía hacerlo llamando
    directamente a la API, aunque no hubiera pantalla para eso). La lectura (`GET`) se
    mantiene abierta a cualquier usuario autenticado, porque el mozo necesita ver la carta.
  - **Validación de categoría al crear/editar un producto**: antes, mandar un `categoriaId`
    inexistente producía un error 500 (`DbUpdateException` por violar la clave foránea) en
    vez de un 400 claro. Ahora `CreateProductoDtoValidator`/`UpdateProductoDtoValidator`
    verifican que la categoría exista **y esté activa** antes de guardar.
- No se agregaron columnas nuevas, así que **no hace falta una migración** para esta fase.

### Frontend

- Nueva página `/admin/menu` (`pages/AdminMenu.jsx`), protegida por rol: solo ADMIN puede
  entrar. Desde el Dashboard, el ADMIN ve un botón adicional ("Administrar carta") junto al
  de caja para llegar ahí.
- Dos pestañas: **Categorías** (crear, editar, desactivar/reactivar) y **Productos** (crear,
  editar, desactivar/reactivar, con selector de categoría activa, precio y un campo "Tipo"
  libre con sugerencias — Plato, Bebida, Entrada, Postre, Acompañamiento).
- El panel de admin lista **todo**, incluyendo categorías/productos inactivos (para poder
  reactivarlos); el mozo sigue viendo solo lo activo/disponible, como antes.
- Con esto, el catálogo ya no depende de crear categorías/productos a mano por Postman: el
  administrador puede cargar toda la carta real de "Cara Negra" desde la app.

## 9. Fase 4 — Panel administrador y reportes

### Backend

- Nuevo `ReportesController` (`/reportes/resumen-ventas`, `/reportes/productos-mas-vendidos`),
  **solo ADMIN**, con dos consultas nuevas:
  - `GetResumenVentasQuery`: total cobrado (suma de pagos activos, sin contar anulados) en un
    rango de fechas, cantidad de pedidos (y cuántos se cancelaron), cantidad de pedidos con
    al menos un pago, ticket promedio, y el desglose de ventas por método de pago.
  - `GetProductosMasVendidosQuery`: top N productos por cantidad vendida en el rango, excluyendo
    ítems y pedidos cancelados (un producto pedido pero nunca cobrado no cuenta como "vendido").
- El CRUD de `MesasController` **ya existía**; el único cambio fue restringir
  crear/editar/desactivar una mesa a rol ADMIN (antes cualquier usuario autenticado podía
  hacerlo). El mozo y caja siguen pudiendo leer el mapa de mesas sin restricción — el sistema
  sigue cambiando el estado de las mesas automáticamente al tomar/cobrar pedidos, eso no cambió.
- No se agregaron columnas nuevas, así que **no hace falta una migración** para esta fase.

### Frontend

- El botón "Administrar carta" del Dashboard ahora abre un **hub** (`/admin`,
  `pages/AdminHub.jsx`) con tres secciones: Carta y menú (Fase 3, sin cambios), Mesas y
  Reportes de ventas.
- Nueva página `/admin/mesas` (`pages/AdminMesas.jsx`): crear mesas por número, editar
  número/estado, y un botón rápido para poner en mantenimiento/reactivar. El selector de
  estado incluye una nota aclarando que el sistema ya cambia el estado solo al tomar/cobrar
  pedidos — es solo para corregir una mesa que quedó "trabada".
- Nueva página `/admin/reportes` (`pages/AdminReportes.jsx`): selector de rango (Hoy, Últimos
  7 días, Este mes, o fechas personalizadas) con tarjetas de resumen (total vendido, ticket
  promedio, pedidos, cancelados), desglose de ventas por método de pago y un ranking de los
  10 productos más vendidos.

## 10. Fase 5 — Administración de usuarios y roles

### Backend

- **Corrección de seguridad importante**: `LoginCommand` ahora verifica `usuario.EsVerificado`
  antes de emitir el token. Antes de este cambio, desactivar a un usuario desde
  `DELETE /usuarios/{id}` (que pone `EsVerificado = false`) **no le impedía seguir
  iniciando sesión** — el login nunca revisaba ese campo. Si ya desactivaste algún usuario
  de prueba esperando que quedara bloqueado, ahora sí queda bloqueado correctamente.
- El CRUD de `UsuariosController` (crear, editar, desactivar/reactivar, resetear contraseña,
  listar con búsqueda) **ya existía** de una fase anterior — no hizo falta tocarlo.
- Se agregó `RolesController` (`GET /roles`, solo ADMIN) de solo lectura, para que el panel
  pueda mostrar los roles disponibles (ADMIN, MOZO, CAJERO) al crear/editar un usuario. **No
  hay crear/editar/borrar rol**: la autorización de toda la aplicación usa el nombre del rol
  hardcodeado en cada controlador (p.ej. `[Authorize(Roles = "ADMIN")]`), así que un rol nuevo
  no tendría ningún permiso real asociado — agregar esa función habría sido decorativa y
  potencialmente confusa. Si en el futuro se necesitan roles verdaderamente personalizables,
  eso requiere rediseñar la autorización (permisos por rol en base de datos, no por nombre).
- No se agregaron columnas nuevas, así que **no hace falta una migración** para esta fase.

### Frontend

- Nueva página `/admin/usuarios` (`pages/AdminUsuarios.jsx`), agregada como cuarta sección del
  hub de administración (`/admin`). Permite: buscar por nombre, crear un usuario (usuario,
  nombre completo, contraseña, rol), editar nombre/rol, desactivar/reactivar, y resetear la
  contraseña de cualquier usuario sin necesitar la contraseña actual.
- Por seguridad, un ADMIN no puede desactivar su propia cuenta desde este panel (el botón se
  deshabilita para su propio usuario), para evitar quedarse bloqueado fuera del sistema.

## 11. Fase 6 — Inventario, PWA, ticketera y despliegue

Esta es la última fase del plan original. Cubre cuatro cosas independientes: activar el
módulo de inventario, imprimir la comanda en cocina automáticamente, convertir el frontend
en una app instalable (PWA), y dejar todo listo para desplegar con Docker.

### Backend — Inventario

- Las tablas `Articulo` y `MovimientoArticulo` **ya existían** en el esquema de base de datos
  desde una fase anterior, pero no tenían ningún endpoint que las usara. Esta fase agrega el
  CRUD completo: `ArticulosController` (**todo el controlador es ADMIN-only**, a diferencia de
  Categorías/Productos — el stock de insumos es información sensible que mozo/caja no
  necesitan ver).
- **El stock nunca se edita directamente**: `UpdateArticuloDto` deliberadamente no incluye el
  campo `Stock`. Todo cambio de stock pasa por `POST /articulos/{id}/movimientos`, que registra
  un `MovimientoArticulo` con:
  - **Entrada**: suma la cantidad al stock actual (compra/reposición de insumos).
  - **Salida**: resta la cantidad (valida que haya stock suficiente; si no, error 400).
  - **Ajuste**: **no es una suma/resta** — fija el stock al valor exacto indicado (para
    corregir después de un conteo físico).

  Cada movimiento guarda un `Balance` (el stock resultante), así queda un historial completo
  tipo "estado de cuenta bancario" de cada insumo.
- No se agregaron columnas ni tablas nuevas, así que **no hace falta una migración** para esta
  parte — las tablas ya estaban en el esquema, solo estaban sin usar.

### Backend — Ticketera (impresión de comanda en cocina)

- Se agregó `IImpresoraCocinaService` / `ImpresoraCocinaService`: al crear un pedido o agregar
  un ítem a uno existente, el backend intenta imprimir automáticamente la comanda en una
  impresora térmica de cocina conectada por red (protocolo ESC/POS, puerto 9100/JetDirect
  estándar), usando una conexión TCP directa — **no se agregó ningún paquete NuGet nuevo**.
- **Deshabilitada por defecto.** Para activarla, edita `appsettings.json` (o mejor,
  `appsettings.Production.json` / variables de entorno, para no versionar la IP real) con la
  sección:
  ```json
  "ImpresoraCocina": {
    "Habilitada": true,
    "Ip": "192.168.1.50",
    "Puerto": 9100,
    "TimeoutMs": 3000
  }
  ```
  Reemplaza `192.168.1.50` por la IP real de la impresora en tu red local (revisa el manual de
  la impresora o su panel de configuración para encontrarla). Si usas Docker, estas mismas
  claves se configuran vía variables de entorno `ImpresoraCocina__Habilitada`,
  `ImpresoraCocina__Ip`, `ImpresoraCocina__Puerto` (ver `.env.example`).
- **Este servicio nunca hace fallar la toma de pedidos.** Si la impresora está apagada,
  desconectada, o mal configurada, el error queda registrado en los logs (`logs/caranegra-.log`)
  y el mozo/caja pueden seguir trabajando con normalidad — solo no sale el ticket.
- Por defecto, el ticket se imprime en ASCII plano sin tildes/`ñ` (se normalizan a su letra
  base: "café" → "cafe"), porque muchas impresoras térmicas económicas no traen configurada una
  tabla de caracteres en español. Si tu impresora sí soporta una codepage con español (Latin-1/
  CP858), puedes cambiar `Encoding.ASCII` por `Encoding.Latin1` en
  `src/CaraNegra.API/Impresion/ImpresoraCocinaService.cs` (método `ConstruirTicket`) y las
  tildes/`ñ` saldrán correctamente.
- Se agregó `POST /pedidos/{id}/reimprimir` para reimprimir manualmente la comanda completa de
  un pedido (por ejemplo, si la impresora estaba sin papel cuando se tomó el pedido). En el
  frontend, hay un botón "Reimprimir comanda" en la pantalla de confirmación del pedido
  (`OrderSuccess.jsx`).

### Frontend — Inventario

- Nueva página `/admin/inventario` (`pages/AdminInventario.jsx`), quinta sección del hub de
  administración. Permite: listar artículos (con categoría, tipo, precio, stock y estado
  activo/inactivo), crear/editar artículos, y — para cada artículo — ver su historial de
  movimientos y registrar uno nuevo (Entrada/Salida/Ajuste, con cantidad, referencia y notas
  opcionales).

### PWA (app instalable)

- Se agregaron `public/manifest.webmanifest`, iconos (`icon-192.png`, `icon-512.png`,
  `icon-512-maskable.png`, `apple-touch-icon.png`, `favicon.svg` — antes `favicon.svg` estaba
  referenciado en `index.html` pero el archivo no existía) y un service worker
  (`public/sw.js`), registrado desde `main.tsx` **solo en builds de producción** (en
  desarrollo, con Vite + HMR, un service worker activo puede servir versiones cacheadas viejas
  y confundir al recargar).
- **A propósito, el service worker NUNCA cachea nada de `/api/**` ni `/hubs/**`**: este es un
  sistema de pedidos, caja e inventario en vivo — mostrar un stock, un estado de mesa o un
  pedido "viejo" desde caché sería peor que no tener conexión. Solo se cachea el cascarón
  estático (HTML/JS/CSS/iconos) para que la app abra rápido y quede instalable en el celular o
  tablet del mozo/caja.
- Para probarlo: hay que compilar en modo producción (`npm run build` + servir `dist/`, no
  `npm run dev`) — los navegadores exigen HTTPS o `localhost` para registrar un service worker,
  y en `npm run dev` está deshabilitado a propósito. Al abrir la app en Chrome/Edge en el
  celular debería aparecer la opción "Agregar a pantalla de inicio" / "Instalar app".

### Despliegue (Docker)

- `src/CaraNegra.API/Dockerfile`: build multi-etapa con el SDK de .NET 10, publicado sobre la
  imagen runtime `aspnet:10.0` (más liviana, sin el SDK completo). Expone el puerto 8080.
- `cara-negra-frontend/Dockerfile`: build multi-etapa con Node 22, sirviendo el resultado de
  `npm run build` con nginx (`nginx.conf` incluido, con *fallback* de rutas para React Router y
  cabeceras `no-cache` para `sw.js`/`manifest.webmanifest` — para que una actualización del
  service worker se note de inmediato en vez de quedar atascada en una versión vieja cacheada).
  **Importante**: `VITE_API_URL` queda "horneada" dentro del bundle de JS en tiempo de build
  (no es una variable de entorno de runtime) — si cambia la URL/dominio de la API, hay que
  reconstruir la imagen del frontend, no solo reiniciar el contenedor.
- `docker-compose.yml` (en la raíz) levanta los 3 componentes: `db` (MySQL 8), `api` y
  `frontend`. Variables sensibles (contraseñas, secreto JWT, IP de la impresora) se configuran
  en un archivo `.env` (**no se sube a git**) a partir de `.env.example`.
- **Pasos para desplegar:**
  1. `cp .env.example .env` y completar los valores reales (contraseña de MySQL, un
     `JWT_SECRET` largo y aleatorio — por ejemplo con `openssl rand -base64 48` —, y
     opcionalmente la IP de la impresora de cocina).
  2. `docker compose up -d --build`.
  3. **Aplicar el esquema de base de datos**: como la imagen de runtime del backend no incluye
     el SDK de .NET (por diseño, para que sea más liviana), las migraciones no se aplican
     solas dentro del contenedor. Con el puerto 3306 expuesto por `docker-compose.yml`, desde
     una máquina que sí tenga el SDK de .NET (como esta misma, `finan13`) se puede correr, desde
     `src/CaraNegra.API`:
     ```
     dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project . --connection "Server=localhost;Port=3306;Database=caranegra;User=caranegra;Password=<la del .env>;"
     ```
  4. La API queda disponible en `http://localhost:8080` y el frontend en
     `http://localhost:8081`. Para un servidor real (no solo la red local del restaurante),
     falta poner ambos detrás de HTTPS (por ejemplo con un reverse proxy como Caddy o Nginx +
     Certbot delante de estos dos puertos) — eso ya depende del hosting/dominio que se use, y
     queda fuera del alcance de este `docker-compose.yml` de referencia.

Con esto queda completo el plan original de las 6 fases para el sistema de "Cara Negra".
