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
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=TU_PASSWORD;"
dotnet user-secrets set "JwtSettings:Secret" "una-clave-larga-de-al-menos-32-caracteres-para-la-demo"
```

### 1.3 Migración de base de datos

La migración incluye la tabla `Comprobantes`. Desde `src\CaraNegra.API`:

```powershell
$env:RESTAURANT_DB_CONNECTION = "Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=TU_PASSWORD;"
dotnet ef migrations add FacturacionElectronicaYCocina --project ../CaraNegra.Infrastructure --startup-project .
dotnet ef database update --project ../CaraNegra.Infrastructure --startup-project .
```

### 1.4 Datos del local (lo que sale impreso en boleta y factura)

Edita la sección `Empresa` de `src\CaraNegra.API\appsettings.json`. Pon ahí el RUC, la razón social, el nombre comercial y la dirección del cliente al que le vas a presentar la demo. Las series por defecto son `B001` (boletas) y `F001` (facturas).

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
