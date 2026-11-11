# Demo del sistema: facturación electrónica y pantalla de cocina

Este proyecto (`restaurant`) es una copia de **cara_negra** a la que se le agregó:

1. **Facturación electrónica simulada**: boletas y facturas con el formato de SUNAT (serie y correlativo, IGV, monto en letras y QR).
2. **Módulo de cocina**: una pantalla en tiempo real (KDS) que funciona **junto con** la comanda impresa de siempre.

> ⚠️ **Los comprobantes son una SIMULACIÓN.** No se firman con un certificado digital ni se envían a SUNAT, así que no tienen validez tributaria. Por eso cada boleta y factura lleva impresa la leyenda **"DEMO – SIN VALIDEZ TRIBUTARIA"**. Para usarlos en real hay que contratar un OSE o PSE (fase 2).

---

## 1. Preparación (una sola vez)

### 1.1 Base de datos separada para la demo

La demo **no debe usar la base de datos de Cara Negra**. Crea una nueva en MySQL:

```sql
CREATE DATABASE restaurant_demo CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Este proyecto tiene su propio `UserSecretsId` y ya no lee la variable `CARANEGRA_DB_CONNECTION`. Así, ni la app ni las migraciones pueden tocar por error la base de datos de Cara Negra.

### 1.2 Secretos del backend

Desde `src\CaraNegra.API`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=Mina@200717;"
dotnet user-secrets set "JwtSettings:Secret" "W5mX9kR3pA8vT2zN4bC7xF1sQ6jL0eYd"
```

### 1.3 Migración de base de datos

La migración incluye la tabla `Comprobantes`. Desde `src\CaraNegra.API`:

```powershell
$env:RESTAURANT_DB_CONNECTION = "Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=Mina@200717;"
dotnet ef migrations add FacturacionElectronicaYCocina --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

### 1.3.1 Migraciones posteriores

Cada vez que se agregan tablas o columnas nuevas, hay que generar y aplicar su migración desde `src\CaraNegra.API`, con la variable `RESTAURANT_DB_CONNECTION` definida (ver 1.3):

```powershell
dotnet ef migrations add ClientesYAtencionMesas --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

### 1.4 Datos del local (lo que sale impreso en boleta y factura)

Entra con el usuario ADMIN a **Admin → Datos de la empresa** y completa:
- RUC. El botón **SUNAT** trae la razón social y la dirección.
- Nombre comercial.
- Teléfono y correo.
- **Series** de boletas y facturas.
- **% de IGV**.
- Mensaje al pie.
- **Logo**.

Estos datos se guardan en la base de datos (tabla `ConfiguracionEmpresa`) y se usan en:
- boletas y facturas
- recibos
- la comanda de cocina
- la pantalla de login
- el título de la aplicación

Para instalar el sistema en **otro restaurante** no hay que cambiar código: basta con otra base de datos y llenar esta pantalla.

La sección `Empresa` de `appsettings.json` solo sirve como valor inicial la primera vez que arranca el sistema con la tabla vacía.

### 1.5 Frontend

Desde `cara-negra-frontend` (se agregó la librería `qrcode`):

```powershell
npm install
npm run dev
```

### 1.6 Usuarios para la demo

Al arrancar, el backend crea el usuario `admin` (la contraseña temporal sale en el log) y crea también el rol nuevo **COCINA**. Desde *Admin → Usuarios y roles* crea estos usuarios:

| Usuario | Rol | Dónde se usa |
|---|---|---|
| mozo1 | MOZO | Celular: toma de pedidos |
| cocina1 | COCINA | Tablet o TV: pantalla de cocina (entra directo a `/cocina`) |
| caja1 | CAJERO | PC de caja: cobro y comprobantes |

Carga también algunas mesas y productos de la carta.

---

## 2. Guion de la presentación (unos 10 minutos)

Abre tres ventanas o dispositivos: el **mozo** (celular), la **cocina** (tablet o TV, pantalla completa con el botón ⛶) y la **caja** (PC).

1. **Toma de pedido (mozo).** Elige una mesa, agrega 2 o 3 platos y escribe una nota, por ejemplo "sin cebolla". Envía el pedido.
2. **Cocina en vivo.** El pedido aparece al instante en la columna **Nuevos**, con un sonido de aviso (actívalo primero con el botón 🔊). Al mismo tiempo, la ticketera imprime la comanda como siempre.
   - Muestra el **cronómetro**: se pone amarillo a los 10 minutos y rojo a los 20.
   - Pulsa **Empezar**: el pedido pasa a **En preparación**.
   - Toca los platos uno por uno para marcarlos listos. Cuando el último está listo, el pedido pasa solo a **Listos para servir**.
   - En el celular del mozo aparece el aviso: *"Mesa X: pedido listo para servir"*.
3. **Pedido adicional.** Desde el celular, agrega un postre a esa misma mesa. En cocina, el pedido vuelve a **En preparación** y el postre aparece marcado **ADICIONAL**.
4. **Cobro (caja).** Cobra con un **pago mixto**, por ejemplo una parte en efectivo y el resto con Yape.
5. **Boleta electrónica.** Al completar el pago se abre la ventana de **comprobante electrónico**. Elige *Boleta*, luego *DNI*, y usa "Autocompletar con datos de ejemplo". Pulsa **Emitir boleta electrónica** y muestra:
   - el número `B001-00000001`
   - el desglose de op. gravada, IGV 18% y total
   - el monto en letras
   - el **QR** con el formato de SUNAT
   - la leyenda de demo
6. **Factura.** Repite el flujo con otro pedido, pero elige *Factura* e ingresa un RUC. El RUC se valida en pantalla con su dígito verificador; si pones uno inválido, lo rechaza. La factura queda como `F001-00000001`.
7. **Registro.** En *Caja → Comprobantes* (o *Admin → Comprobantes electrónicos*) se ve el día: número de boletas y facturas, base imponible, IGV y total facturado. Desde ahí se puede reimprimir cualquier comprobante.

**Datos de ejemplo:** DNI `45678912`, RUC `20555666773` (tiene el dígito verificador correcto, pero es inventado).

---

## 3. Qué se agregó (resumen técnico)

**Backend**
- `Comprobante` (entidad), `TipoComprobante`, `TipoDocumentoIdentidad` y `EstadoComprobante`.
- `POST/GET /api/v1/comprobantes` y `GET /api/v1/comprobantes/pedido/{id}`, con acceso para CAJERO y ADMIN.
- Validaciones de SUNAT:
  - el RUC se revisa con el algoritmo módulo 11
  - el DNI debe tener 8 dígitos
  - la factura exige RUC y razón social
  - una boleta de más de S/ 700 exige documento del cliente
- Cada pedido tiene un solo comprobante, y la numeración es única por serie.
- `GET /api/v1/cocina/pedidos`, junto con `PATCH .../items/{id}/estado` y `PATCH .../pedidos/{id}/estado`, con acceso para COCINA y ADMIN.
- El estado del pedido se calcula a partir de sus platos (`CocinaEstadoHelper`).
- El evento en tiempo real `DetalleEstadoCambiado` y el grupo de SignalR "Cocina".
- Ahora se puede agregar un plato a un pedido que ya está "Listo": el pedido vuelve a "En preparación".

**Frontend**
- `/cocina` (pantalla de cocina).
- `/comprobantes` (registro).
- En Caja, la ventana **Emitir comprobante** y el ticket **ComprobanteElectronico** (80 mm, se imprime con `window.print()`).
- El recibo interno de siempre sigue disponible con la opción "Solo recibo interno".

**Fase 2 (para usarlo en real):** integración con un OSE o PSE (Nubefact, Efact, etc.), certificado digital, notas de crédito y comunicación de baja. El punto de integración es `EmitirComprobanteCommandHandler`.

---

## 4. Consulta de DNI / RUC y base de clientes

Al emitir una boleta con DNI o una factura con RUC, el nombre o la razón social se completan solos:

1. Primero se busca en la **base de clientes** del sistema (tabla `Clientes`). Es gratis e instantáneo.
2. Si el cliente es nuevo, se consulta la **API externa** (RENIEC para DNI, SUNAT para RUC) y el resultado se guarda en `Clientes`. La próxima compra de ese cliente ya no gasta una consulta de la API.
3. Si la API no encuentra el documento, o no responde, el cajero escribe el nombre a mano. Al emitir el comprobante, ese cliente también queda guardado.

En `Clientes` se guarda el tipo de documento, el número, el nombre o razón social y, en los RUC, la **dirección fiscal**. La dirección se completa sola en la factura y se puede corregir a mano; la corrección queda guardada. Si un RUC se guardó antes sin dirección, la primera vez que se vuelve a consultar se pide una sola vez a SUNAT.

La columna de la dirección necesita esta migración (desde `src\CaraNegra.API`, ver 1.3):

```powershell
dotnet ef migrations add ClientesDireccion --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

**Configuración** (sección `ConsultaDocumentos` de `appsettings.json`). Se usa **[Decolecta](https://decolecta.com/)** para DNI y RUC:

| Consulta | Petición | Datos que se usan |
|---|---|---|
| DNI (RENIEC) | `GET https://api.decolecta.com/v1/reniec/dni?numero=46027897` | `full_name` (ej. "DELGADO HUAMANI ROXANA KARINA") |
| RUC (SUNAT) | `GET https://api.decolecta.com/v1/sunat/ruc?numero=20601030013` | `razon_social`, `estado`, `condicion`, y `direccion` + `distrito` + `provincia` + `departamento` |

Todas las peticiones llevan el header `Authorization: Bearer <token>`.

- `"Proveedor": "Decolecta"` (así viene): consulta real. Necesita el token.
- `"Proveedor": "Simulado"`: sin internet ni token, para presentar sin conexión. Solo reconoce los documentos de ejemplo de la demo (DNI 45678912 y RUC 20555666773).

Guarda el token de Decolecta **fuera** de `appsettings.json`. Ejecuta esto desde `src\CaraNegra.API`:

```powershell
dotnet user-secrets set "ConsultaDocumentos:Token" "TU_TOKEN"
```

Si no hay token, o Decolecta responde error, el sistema avisa en pantalla y el cajero escribe el nombre a mano. Con un 404 el documento se da por no encontrado. Con un 401 o un 429 aparece el aviso "token inválido" o "consultas agotadas".

**Endpoint:** `GET /api/v1/clientes/consulta?tipo=DNI&numero=12345678`, con acceso para CAJERO y ADMIN. Responde:
- **200** con el nombre y la fuente (`Clientes` o el proveedor)
- **404** si el documento no existe
- **503** si la API no está disponible

---

## 5. Presentar la demo desde otra computadora (túnel, sin hosting)

El sistema sigue corriendo en **esta PC** (backend, MySQL y frontend). Un **túnel de Cloudflare** le da una dirección pública temporal `https://xxxx.trycloudflare.com`, y la laptop (Mac), los celulares y la tablet solo tienen que abrir esa dirección en el navegador, desde cualquier red. En la Mac no se instala nada, y no hace falta cuenta ni pagar.

Cómo viaja cada petición: navegador → túnel → frontend en la PC (`127.0.0.1:4173`) → el frontend reenvía `/api` y `/hubs` al backend (`localhost:5014`). El backend y MySQL nunca quedan expuestos directamente.

### 5.1 Una sola vez

Instala `cloudflared` en la PC (PowerShell) y cierra la terminal:

```powershell
winget install --id Cloudflare.cloudflared
```

Además, instala **Escritorio remoto de Chrome** en la PC (https://remotedesktop.google.com/access) con tu cuenta de Google. Es el respaldo: si algo se cae, entras a la PC desde la Mac, vuelves a lanzar la demo y ves la nueva dirección.

### 5.2 El día de la demo, antes de salir de casa

1. Configura la PC para que **no se suspenda**: *Configuración → Sistema → Inicio/apagado → Suspender: Nunca* (o `powercfg /change standby-timeout-ac 0`). Déjala enchufada y con internet.
2. Verifica que MySQL esté encendido.
3. Doble clic en **`iniciar-demo.bat`** (raíz del proyecto). Se abren 3 ventanas: backend, frontend y túnel.
4. En la ventana **"3 - Tunel"** aparece la dirección `https://....trycloudflare.com`. Cópiala y mándatela por WhatsApp.
5. **Prueba desde tu celular con datos móviles** (no con el wifi de casa): entra con `mozo1`, abre una mesa y verifica que en la cocina aparezca el pedido en tiempo real.

### 5.3 En el restaurante

Abre la dirección en la Mac (Chrome o Safari), en el celular del mozo y en la tablet de cocina. Cada dispositivo entra con su usuario, igual que en la sección 2.

### 5.4 Tener en cuenta

- **La dirección cambia cada vez que se reinicia el túnel.** Si la PC se reinicia o se corta la luz o el internet de la casa, hay que volver a ejecutar `iniciar-demo.bat` (por Escritorio remoto de Chrome) y usar la nueva dirección.
- Depende del internet de la casa y del restaurante. Lleva datos móviles para compartir desde el celular por si el wifi del local falla.
- La dirección es pública: quien la tenga llega a la pantalla de login (sin usuario y contraseña no entra). Usa contraseñas no triviales y **cierra las 3 ventanas al terminar la demo**.
- La **comanda impresa** de cocina no sale en el restaurante: la ticketera tendría que estar en la red de la PC. La pantalla de cocina sí funciona. Las boletas y facturas se imprimen desde la Mac con `window.print()`.
- La consulta de DNI/RUC (Decolecta) funciona igual: la hace la PC.
- En modo demo el backend admite 600 peticiones por minuto por equipo (`RateLimiting` en `appsettings.Development.json`), porque en el restaurante todos los dispositivos salen a internet con la misma IP.
- Si pruebas en la PC, entra por `http://127.0.0.1:4173`. `npm run dev` (puerto 5173) sigue funcionando como siempre para programar.

### 5.5 Alternativa con dirección fija: Tailscale Funnel

Si prefieres una dirección que **no cambie**, usa Tailscale (cuenta gratuita):

1. Instala Tailscale en la PC e inicia sesión.
2. Con la demo levantada (ventanas 1 y 2), ejecuta:

   ```powershell
   tailscale funnel 4173
   ```

   La primera vez, el comando te da un enlace para activar HTTPS y Funnel en tu cuenta.
3. La dirección queda fija, del tipo `https://nombre-de-tu-pc.tailXXXX.ts.net`.

El frontend ya acepta ese dominio.

---

## 6. Ingreso rápido con PIN (mozos y cocina)

Los mozos y la cocina pueden entrar tocando su nombre y escribiendo un **PIN de 4 dígitos**, en lugar de usuario y contraseña. Solo funciona en los **celulares y tablets que registre el ADMIN**. Desde cualquier otro equipo (por ejemplo, alguien con la dirección del túnel) se sigue pidiendo usuario y contraseña. **CAJERO y ADMIN siempre entran con contraseña.**

### 6.1 Migración (una sola vez)

Desde `src\CaraNegra.API`, con `RESTAURANT_DB_CONNECTION` definida (ver 1.3):

```powershell
dotnet ef migrations add PinYDispositivos --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Crea la tabla `DispositivosAutorizados` y agrega a `Usuarios` las columnas del PIN.

### 6.2 Habilitarlo

1. **Registrar cada dispositivo.** En el celular o tablet, entra con el usuario ADMIN, ve a **Admin → Dispositivos con PIN**, ponle un nombre ("Tablet terraza", "Celular de Juan") y pulsa **Registrar este dispositivo**. Luego cierra sesión.
   - En una **tablet compartida**, elige "cerrar sesión a los 5 min sin uso": así el siguiente mozo no usa la sesión del anterior.
   - En el **celular personal** de un mozo, elige "Nunca".
2. **Asignar PINs.** En **Admin → Usuarios**, pulsa el ícono de cuadrícula junto a cada MOZO o COCINA y escribe su PIN. Díselo en persona. No se aceptan PINs obvios como 0000, 1111, 1234 o 4321.
3. Al abrir el sistema en ese dispositivo aparece **"¿Quién eres?"**: el mozo toca su nombre, escribe el PIN y entra.

### 6.3 Seguridad

- En la base de datos solo se guarda el **hash** del PIN (BCrypt, igual que las contraseñas) y el **hash** del token del dispositivo.
- Tras **5 PIN incorrectos seguidos**, ese PIN se **bloquea 10 minutos**. Mientras tanto, el usuario puede entrar con su contraseña o el ADMIN puede desbloquearlo desde *Usuarios* (aparece "PIN bloqueado").
- Si un celular se pierde o un mozo deja el restaurante, bórralo en **Dispositivos con PIN** (**Revocar**). Desde ese momento ese equipo ya no puede entrar con PIN.
- Si otro mozo entra en la misma tablet, no ve el pedido a medio armar del anterior. Si vuelve el mismo mozo, lo recupera.

---

## 7. Facturación electrónica real con APISUNAT (sandbox → producción)

APISUNAT (Lucode) es un **PSE**: recibe los datos del comprobante, arma el XML, lo firma y lo envía a SUNAT. No hace falta certificado digital propio. El sistema tiene tres modos, que se eligen en la sección `FacturacionElectronica` de `appsettings.json`:

| Modo | `Proveedor` | `Ambiente` | Qué pasa | Leyenda impresa |
|---|---|---|---|---|
| Demo | `Simulado` (así viene) | — | No sale a internet | DEMO – SIN VALIDEZ TRIBUTARIA |
| Pruebas | `ApiSunat` | `Sandbox` | Se envía al sandbox de APISUNAT (no llega a SUNAT) | PRUEBA (SANDBOX) – SIN VALIDEZ TRIBUTARIA |
| Real | `ApiSunat` | `Produccion` | Se envía a SUNAT | (ninguna) |

### 7.1 Migración (una sola vez)

Desde `src\CaraNegra.API`, con `RESTAURANT_DB_CONNECTION` definida (ver 1.3):

```powershell
dotnet ef migrations add FacturacionApiSunat --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Agrega a `Comprobantes` estas columnas: ambiente, mensaje de SUNAT, enlaces a XML, CDR y PDF, e intentos de envío.

### 7.2 Probar en el sandbox

1. Crea tu cuenta en **https://app.apisunat.pe** y registra la empresa (RUC). En **Organizaciones** copia el **token de pruebas (sandbox)**.
2. Guarda el token **fuera** de `appsettings.json`. Desde `src\CaraNegra.API`:
   ```powershell
   dotnet user-secrets set "FacturacionElectronica:Token" "TU_TOKEN_SANDBOX"
   ```
3. En `appsettings.json` (o `appsettings.Development.json`), cambia:
   ```json
   "FacturacionElectronica": { "Proveedor": "ApiSunat", "Ambiente": "Sandbox" }
   ```
4. En **Admin → Datos de la empresa**, el **RUC** debe ser el mismo que registraste en APISUNAT.
5. Reinicia el backend y cobra un pedido. Al emitir, el comprobante muestra su **estado en SUNAT** y el enlace **"PDF del proveedor"**.

**Casos para probar en el sandbox:**
- Boleta sin documento.
- Boleta con DNI.
- Factura con RUC.
- Un pedido **con descuento** (revisa que el total del PDF del proveedor coincida con lo cobrado).
- Desconecta el internet de la PC y emite: el comprobante debe quedar **"Por enviar"**. Reconecta: en unos 2 minutos debe pasar solo a **Aceptado**.

### 7.3 Estados del comprobante

| Estado | Significado | Qué hacer |
|---|---|---|
| **Aceptado** | SUNAT lo aceptó (tiene CDR) | Nada |
| **Pendiente** | Firmado y enviado; SUNAT aún no confirma | Nada: el sistema lo consulta solo |
| **Por enviar** | No llegó al proveedor (sin internet, caída, token) | Nada: se reintenta solo cada 2 min, o pulsa **Reintentar envío** |
| **Rechazado** | SUNAT lo rechazó por datos incorrectos | **Emitir de nuevo** con los datos corregidos (nuevo número) |
| **Error de envío** | APISUNAT no aceptó los datos | Lee el mensaje; **Reintentar** o **Emitir de nuevo** |

La venta **nunca se bloquea**: el número se reserva siempre y, si no hay internet, el comprobante se envía después. En *Comprobantes* aparece un aviso cuando hay comprobantes por confirmar o con error. Los rechazados y los que tienen error no suman en los totales.

### 7.4 Pasar a producción

1. En APISUNAT, autoriza a APISUNAT como tu **PSE** en el portal de SUNAT (con la clave SOL) y copia el **token de producción**.
2. Guarda el nuevo token:
   ```powershell
   dotnet user-secrets set "FacturacionElectronica:Token" "TU_TOKEN_PRODUCCION"
   ```
3. Cambia a `"Ambiente": "Produccion"` y reinicia.
4. Usa **series nuevas** (por ejemplo `B002` y `F002`) o continúa la numeración que el restaurante ya tenga en SUNAT, para no repetir números de las pruebas.

Los comprobantes emitidos en sandbox **nunca** se reenvían a producción. El sistema lo impide.

**Pendiente para una siguiente fase:** anulación de boletas (resumen diario) y de facturas (comunicación de baja), y notas de crédito.

---

## 8. Jornada (apertura del día) y cierre de caja

Cada día se trabaja en una **jornada**: se **abre** al empezar, con el fondo de caja, y se **cierra** al final con **lo registrado en el sistema** (sin arqueo). La jornada no depende del día calendario: si se atiende pasada la medianoche, esas ventas siguen siendo de la jornada abierta. Cada pedido y cada pago quedan registrados en su jornada.

### 8.1 Migración (una sola vez)

Desde `src\CaraNegra.API`, con `RESTAURANT_DB_CONNECTION` definida (ver 1.3):

```powershell
dotnet ef migrations add JornadasYCierreCaja --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Crea las tablas `Jornadas`, `JornadaArqueos` y `MovimientosCaja`, y agrega `JornadaId` a `Pedidos` y `Pagos`.

### 8.2 Cómo funciona

- **Apertura** (ADMIN o CAJERO). Al entrar sin jornada abierta aparece **"Abrir el día"**: fecha, **fondo de caja** (el sencillo para dar vuelto) y una observación. Con "Ahora no" se puede entrar igual; queda un aviso arriba.
- **Sin jornada abierta:**
  - los mozos ven el aviso "El día aún no se ha abierto";
  - el sistema **no deja tomar pedidos ni cobrar**.
- **Durante el día**, en **Caja → Jornada** (o **Admin → Jornada y cierre de caja**):
  - se registran los **movimientos de caja**: salidas de efectivo (compra de gas, pago a un proveedor…) y entradas (sencillo adicional);
  - el ADMIN ve el resumen en vivo y lo que debería haber por medio de pago;
  - el CAJERO **no ve los montos** hasta cerrar; los ve en el reporte de cierre.
- **Cierre:**
  1. **Revisión:** si hay pedidos sin cobrar, se listan y **no se puede cerrar** hasta cobrarlos o anularlos. Los comprobantes pendientes de SUNAT solo generan un aviso.
  2. **Confirmar:** se escribe una observación opcional y se toca **Sí, cerrar**. **No hay arqueo** (se quitó a pedido del local): la jornada se cierra con lo registrado. Por compatibilidad se guarda "contado = esperado" (diferencia 0) en `JornadaArqueos`; el campo `conteos` del API ya no se usa.
  3. El efectivo que **debe haber** se calcula así: fondo + cobros en efectivo + entradas − salidas. Para detectar faltantes hay que compararlo a mano con la gaveta y el aplicativo de Yape / POS.
  4. Se genera el **reporte de cierre** para imprimir en la ticketera. Incluye:
     - ventas, ticket promedio, descuentos y anulaciones;
     - **caja según el sistema**: lo cobrado y lo que debe haber por medio de pago (los cierres antiguos con diferencias siguen mostrando su arqueo);
     - los movimientos de caja;
     - las ventas por mozo y por salón;
     - los platos más vendidos;
     - el rango de boletas y facturas;
     - espacio para las firmas.
- **Jornada cerrada = solo lectura:** sus pagos y movimientos ya no se pueden anular.
- **Se olvidaron de cerrar:** si la jornada abierta es de un día anterior, aparece un aviso rojo para cerrarla antes de empezar el nuevo día.
- **Historial** (ADMIN): en la misma pantalla de Jornada se ven los cierres anteriores, con su total (y su faltante o sobrante si fueron cerrados con arqueo), y se puede reimprimir cada reporte.

> El medio de pago "Efectivo" se reconoce por su nombre: debe contener la palabra *efectivo*.

---

## 9. Fotos de los platos y buscador de la carta

### 9.1 Migración (una sola vez)

Desde `src\CaraNegra.API`, con `RESTAURANT_DB_CONNECTION` definida (ver 1.3):

```powershell
dotnet ef migrations add ProductosImagenes --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Esto hace dos cambios en la base:

- crea la tabla `ProductoImagenes`;
- agrega la columna `ImagenVersion` a `Productos`, con valor 0, que significa "sin foto".

En el restaurante no hay que hacer nada a mano: `publicar.ps1` incluye la migración en el paquete, y `ACTUALIZAR.bat` la aplica.

### 9.2 Subir fotos (ADMIN)

1. Entra a **Admin → Administrar carta → Productos** y edita un producto. También funciona al crear uno nuevo.
2. En **Foto**, toca **Elegir foto**. En el celular puedes tomarla con la cámara.
3. El navegador la reduce antes de subirla: una foto de celular de 3 a 6 MB queda en unos 40 a 90 KB, y se ve el tamaño final.
4. Pulsa **Guardar**. Para cambiar la foto, elige otra; para borrarla, usa **Quitar foto**.

Las fotos se guardan **en la base de datos**:

- entran solas en el respaldo diario y se restauran con él;
- no se pierden al actualizar el sistema.

Los celulares guardan cada foto en caché y solo la vuelven a descargar cuando se cambia.

### 9.3 Buscador (mozos)

Al abrir la carta de una mesa, arriba aparece **"Buscar plato o bebida…"**:

- **Toda la carta:** busca en todas las categorías mientras se escribe, sin esperar al servidor.
- **Sin tildes ni mayúsculas:** `aji` encuentra "Ají de gallina".
- **Varias palabras en cualquier orden:** `chicha jarra` encuentra "Jarra de chicha morada".
- **Qué revisa:** el nombre, la categoría y la descripción. Lo que coincide se resalta y cada resultado muestra su categoría.
- **Ver la foto:** al tocar la foto de un plato se ve en grande, para mostrársela al cliente, y desde ahí se agrega al pedido.

En **Administrar carta → Productos** hay un buscador igual para encontrar rápido un producto.

---

## 10. Productos quitados de un pedido (registro de anulaciones)

### 10.1 Migración (una sola vez)

Desde `src\CaraNegra.API`, con `RESTAURANT_DB_CONNECTION` definida (ver 1.3):

```powershell
dotnet ef migrations add ProductosAnulados --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Crea la tabla `ProductosAnulados`. En el restaurante no hay que hacer nada a mano: `publicar.ps1` la incluye en el paquete y `ACTUALIZAR.bat` la aplica.

### 10.2 Cómo funciona

- **Quién puede:** el **mozo**, solo en **sus propias mesas** (en las de otro mozo ve un candado 🔒), y el **cajero** o el **admin** en cualquier mesa. Se quita desde la carta de la mesa, con el ícono 🗑, y solo mientras el pedido esté *Pendiente* o *En preparación*. El servidor también lo valida.
- **Qué pide:**
  - el **motivo**, que es obligatorio; hay botones rápidos ("El cliente ya no lo quiere", "Error al tomar el pedido"…) o se escribe otro;
  - **cuántas unidades** quitar: de "3 x Chicha" se puede quitar solo 1.
- **Qué queda registrado:** nada se borra sin dejar rastro. Se guarda quién, cuándo, qué producto y cuántos, el importe, el motivo y en qué estado estaba en cocina (*aún no se preparaba*, *en preparación* o *ya preparado*, que sería una merma).
- **Aviso a cocina:** en la ticketera de cocina sale un ticket **"** ANULADO **"**, con el producto, quién lo quitó, el motivo y **NO PREPARAR**.
- **Dónde se ve:**
  - en la carta de la mesa, en **"Quitados de este pedido"**;
  - en **Caja**, al abrir el pedido;
  - en **Jornada**, en la tarjeta **"Productos quitados"** de todo el día; el cajero no ve importes hasta el cierre;
  - en el **reporte de cierre**, en la sección **"PRODUCTOS QUITADOS DE PEDIDOS"**.

---

## 11. Vista de monitor (caja) y cambio de precio

### 11.1 Migración (una sola vez)

```powershell
dotnet ef migrations add CambiosPrecio --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Crea la tabla `CambiosPrecioPedido`. Si **todavía no** creaste la migración `ProductosAnulados` (sección 10), basta con una sola migración: incluye las dos tablas.

### 11.2 Vista de monitor

En una pantalla de **1024 px o más de ancho** (el monitor de la caja o una PC), la carta de la mesa ocupa toda la pantalla:

- **a la izquierda**, la carta en cuadrícula, con buscador y categorías;
- **a la derecha**, el **listado del pedido**:
  - **Ya enviado a cocina**: cada producto con su precio, y los botones para cambiar precio y quitar;
  - **Por enviar**: lo que se va agregando, con − / + para la cantidad;
  - el **total de la mesa** y el botón **Revisar y enviar**, que lleva a la pantalla de envío con el comentario del pedido y las cremas.

En el celular la carta sigue igual que antes, con la barra **Ver pedido** abajo.

Todas las tarjetas de la carta muestran la **foto** del producto; si aún no tiene, aparece un ícono en su lugar.

### 11.3 Cambiar el precio (solo cajero y admin)

- Con el ícono ✎ el cajero o el admin cambian el precio por unidad:
  - **de lo que está por enviar**: se guarda al enviar el pedido;
  - **de lo ya enviado**: se aplica al instante y admite un motivo opcional ("cortesía", "promoción"…).
- **Cuándo no se puede:** si el pedido ya tiene **pagos registrados** (hay que anularlos primero), ni en un pedido cobrado o cancelado.
- **El mozo no puede:** no ve el ✎, y el servidor rechaza cualquier precio que no venga del cajero o del admin.
- **Qué queda registrado:** quién lo cambió, cuándo, de cuánto a cuánto y el motivo.
- **Dónde se ve:**
  - en **Caja**, al abrir el pedido, en "Precios cambiados";
  - en **Jornada**, en la tarjeta "Precios cambiados";
  - en el **reporte de cierre**, en la sección "PRECIOS CAMBIADOS (vs. carta)".
- La boleta o factura sale con el precio cambiado.

## 12. Tema claro u oscuro

El sistema viene en tema **claro** (fondo gris claro, tarjetas blancas).

- Para cambiarlo: ícono de perfil (arriba a la derecha) → **Apariencia en este equipo** → **Claro** u **Oscuro**.
- Se guarda **en cada equipo** (celular, tablet, PC de caja), no por usuario. Por ejemplo: la caja en claro y la TV de cocina en oscuro.
- No necesita migración ni cambios en el servidor.

## 13. Comentario del pedido

Además de las notas de cada plato, cada pedido puede llevar **un comentario general**: "todo para llevar", "servir juntos", "cliente apurado"… (hasta 300 caracteres).

### 13.1 Migración (una sola vez)

```powershell
dotnet ef migrations add PedidoComentario --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

Agrega la columna `Comentario` a la tabla `Pedidos`. Si todavía no creaste las migraciones de las secciones 10 y 11, una sola migración incluye todo.

### 13.2 Cómo funciona

- **Al enviar el pedido** (pantalla **Revisar y enviar**), en "Comentario del pedido": se tocan las **cremas** y/o se escribe el comentario. Antes las cremas y la indicación se pegaban al primer plato; ahora van como comentario de todo el pedido.
- **Si la mesa ya tiene pedido**, el comentario actual aparece escrito: se puede dejar igual, cambiar o borrar en el mismo envío. Si no se toca, no se modifica.
- **Después**, sin agregar platos: con el ✎ junto al comentario:
  - en la carta de la mesa (celular y monitor);
  - en **Caja**, al abrir el pedido.

  El **mozo** solo puede cambiarlo en **sus mesas**; el cajero y el admin, en cualquiera.
- **Cocina:**
  - en la **comanda** sale arriba, antes de los platos, en letra alta: `NOTA: todo para llevar`;
  - si se cambia después, sale un ticket corto **"NOTA DEL PEDIDO"** solo con el comentario nuevo (si se borra, no se imprime nada);
  - en la **pantalla de cocina** se ve en un recuadro amarillo arriba de la tarjeta.
- **Caja:** se ve en la lista de pedidos y al abrir el pedido. También sale en la **comanda** impresa en caja.
- **No sale** en la **nota de pedido** ni en la boleta o factura.

## 14. Nota de cada plato

Además del comentario del pedido, cada plato puede llevar **su propia nota**: "sin cebolla", "término medio", "poco ají" (hasta 200 caracteres). No necesita migración.

- **Dónde se escribe:**
  - en **Revisar y enviar**, con el botón **Nota** de cada plato;
  - en el **monitor** de la caja, con el ícono de nota de cada plato en "Por enviar".
- El diálogo trae atajos de un toque (Sin cebolla, Sin ají, Para llevar… y las cremas de la carta).
- **Varias unidades:** si hay 3 cuyes y solo uno va "sin ají", en "¿A cuántos de los 3?" se elige 1. Quedan dos líneas: 2 cuyes sin nota y 1 "sin ají".
- **Cocina:** en la comanda sale debajo del plato, en letra alta (`>> sin ají`), y en la pantalla de cocina junto al plato.
- **Se ve también** en la carta de la mesa ("Ya en el pedido"), en el monitor, en **Caja** y en la comanda impresa en caja.
- **No sale** en la nota de pedido ni en la boleta o factura.
- Una vez enviado, la nota de un plato ya no se cambia: si hace falta, se quita el plato (con motivo) y se envía de nuevo con la nota correcta, o se usa el comentario del pedido.

## 15. Botones con nombre, nota de pedido y anular un pedido

No necesita migración.

### 15.1 Pantalla de mesas

- Arriba a la derecha los accesos tienen nombre: **Cocina**, **Administrar** y el botón naranja **Cobrar** (abre Caja). En el celular del admin quedan solo los íconos.
- Cada mesa muestra el salón en letra grande (`PASTO 3`) para leerse de lejos.
- En el listado del pedido (monitor y celular) el nombre del plato sale completo, en varias líneas si hace falta.

### 15.2 Nota de pedido

Se imprime con el formato del local, sin el título "NOTA DE PEDIDO":

- **logo** (o el nombre comercial si no hay logo), **razón social**, **dirección** y **RUC**, tomados de **Admin → Datos de la empresa**;
- "Atendido por" y el cliente genérico (`DNI : 00000000`, `CLIENTES VARIOS`);
- tabla **CANT. / DESCRIPCIÓN / P.U. / TOTAL**, descuento si lo hay y **TOTAL A PAGAR**;
- al pie, `Orden <número> · <mesa>` y la fecha y hora.

El endpoint público `GET /api/v1/empresa/publico` ahora devuelve también `ruc`, `razonSocial` y `direccion` (son públicos: salen en cada boleta).

### 15.3 Anular un pedido completo

- **Quitar el último plato** de un pedido (desde la carta) anula el pedido completo: el cuadro lo avisa en rojo y el botón dice **Anular pedido**.
- **Caja → pedido → Anular pedido** (cajero y admin) anula de una vez un pedido con varios platos.
- **Cerrar jornada:** cada pedido sin cobrar (y sin pagos) tiene **Anular** al lado.
- En todos los casos:
  - el motivo es obligatorio;
  - cada plato queda en el registro de anulaciones (sección 10);
  - cocina recibe **"** PEDIDO ANULADO **"** con **NO PREPARAR**, en formato compacto (platos en letra normal, sin líneas en blanco, para ahorrar papel). Desde Caja o el cierre de jornada, la casilla **"Avisar a cocina"** (marcada por defecto) permite no imprimirlo cuando la cocina aún no empezó esos platos;
  - la mesa queda libre.
- **Reglas:** el mozo solo en sus mesas; con un pago activo no se puede (primero se anula el pago).
- **API:** `POST /api/v1/pedidos/{id}/anular` con `{ "motivo": "...", "avisarCocina": true }` (`avisarCocina` es opcional; si falta, se imprime). `PATCH /pedidos/{id}/estado` ya **no** acepta `Cancelado` (cancelaba sin motivo ni aviso a cocina).

## 16. Guías para el personal

En el menú del usuario (la inicial, arriba a la derecha) están las guías interactivas, que funcionan sin internet:

| Guía | Archivo | La ve |
|---|---|---|
| Guía del administrador | `public/guia-admin.html` | ADMIN |
| Guía rápida del mozo | `public/guia-mozo.html` | MOZO y ADMIN |
| Guía rápida del cajero | `public/guia-cajero.html` | CAJERO y ADMIN |

Son archivos estáticos: `npm run build` los copia a `wwwroot` y `publicar.ps1` los incluye en el paquete.

## 17. Pedidos del día

No necesita migración. Para **cajero y admin**.

- **Dónde:** en **Caja → Pedidos de hoy** y en el panel de administración (**Pedidos del día**).
- **Qué muestra:** todos los pedidos de la jornada abierta (o de la última, si no hay una abierta): **abiertos, cobrados y anulados**, con hora, mesa, mozo, cantidad de platos, total, medios de pago, comprobante y comentario. Se actualiza sola con cada pedido, cobro o anulación.
- **Filtros:** Todos · Abiertos · Cobrados · Anulados (con su cantidad), y buscador por mesa, número de pedido o mozo. Arriba, la cantidad de pedidos y el total cobrado.
- **Al tocar un pedido:** platos con sus notas, descuento, pagos (también los anulados), platos quitados y precios cambiados. Botones para **reimprimir la nota de pedido**, abrir la **boleta / factura** (para reimprimirla) o **ir a cobrar** si sigue abierto.
- **Jornadas anteriores:** solo el admin, con el selector de jornada.
- **API:** `GET /api/v1/jornadas/pedidos?jornadaId=` (ADMIN, CAJERO; el cajero solo la jornada abierta).

