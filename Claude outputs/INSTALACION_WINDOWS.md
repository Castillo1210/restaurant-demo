# Instalación del Restaurante POS en una PC con Windows (servicio de Windows)

Esta guía explica cómo dejar el sistema funcionando en la computadora del restaurante. El sistema queda instalado como un **servicio de Windows**. Esto significa que:

- **Arranca solo** al encender la PC, sin que nadie inicie sesión ni abra ventanas negras.
- **Si se cae, Windows lo vuelve a levantar** automáticamente.
- **Atiende a toda la red del local.** La caja, los celulares de los mozos y la tablet de cocina entran con el navegador a `http://IP-DE-LA-PC`.
- **Se respalda solo todos los días** (copia de la base de datos).

> Tiempo estimado: **1 h 30 min** la primera vez, incluida la instalación de MySQL y la configuración inicial. Imprime la **lista de verificación** de la [sección 14](#14-lista-de-verificación-del-día-de-la-instalación) y llévala.

---

## Índice

1. [Cómo queda armado](#1-cómo-queda-armado)
2. [Requisitos](#2-requisitos)
3. [En la PC de desarrollo: generar el paquete](#3-en-la-pc-de-desarrollo-generar-el-paquete-de-instalación)
4. [En la PC del restaurante: preparar Windows](#4-en-la-pc-del-restaurante-preparar-windows)
5. [Instalar MySQL 8.4](#5-instalar-mysql-84)
6. [Instalar el sistema (INSTALAR.bat)](#6-instalar-el-sistema)
7. [Opcional: llevar la configuración de la demo](#7-opcional-llevar-la-configuración-de-la-demo)
8. [Primer ingreso y configuración inicial](#8-primer-ingreso-y-configuración-inicial)
9. [Celulares, tablet de cocina, caja e impresoras](#9-celulares-tablet-de-cocina-caja-e-impresoras)
10. [Administrar el servicio](#10-administrar-el-servicio)
11. [Respaldos y restauración](#11-respaldos-y-restauración)
12. [Instalar una versión nueva](#12-instalar-una-versión-nueva)
13. [Solución de problemas](#13-solución-de-problemas)
14. [Lista de verificación del día de la instalación](#14-lista-de-verificación-del-día-de-la-instalación)
15. [Anexos: carpetas, comandos y acceso remoto](#15-anexos)

---

## 1. Cómo queda armado

```
                 Red del local (WiFi / cable)
   ┌──────────────────────────────────────────────────────────┐
   │  PC del restaurante (servidor + caja)                     │
   │   ├─ Servicio "MySQL84"        → base de datos            │
   │   └─ Servicio "RestaurantePOS" → sistema (puerto 80)      │
   │         · API + pantallas (todo en el mismo servicio)     │
   │         · respaldo diario automático                      │
   └──────────────────────────────────────────────────────────┘
        ▲                ▲                    ▲
   Celulares mozos   Tablet cocina      Navegador de caja
   http://192.168.1.50 (la IP de la PC)   http://localhost
```

- **Un solo servicio** (`RestaurantePOS`) sirve la API y las pantallas. En la PC del local **no hace falta** Node.js, Visual Studio ni .NET, porque el paquete ya trae todo.
- **MySQL** corre como su propio servicio. `RestaurantePOS` depende de él: Windows lo inicia primero.
- El sistema se instala en `C:\RestaurantePOS`.

---

## 2. Requisitos

**PC del restaurante**

| | Mínimo | Recomendado |
|---|---|---|
| Sistema | Windows 10 u 11, 64 bits | Windows 11 Pro |
| Memoria | 8 GB | 16 GB |
| Disco | 20 GB libres | SSD |
| Red | WiFi | **Cable al router** + IP fija |
| Energía | — | **UPS** (estabilizador con batería) para que un corte de luz no apague la PC |

Además necesitas:

- Usuario de Windows con permisos de **administrador**.
- **Internet** el día de la instalación, para descargar MySQL.
- Una **USB** para llevar el paquete.

**PC de desarrollo** (la tuya, con el código en `D:\restaurant-demo`)

- .NET 10 SDK, Node.js y MySQL con la base `restaurant_demo`, es decir, lo que ya usas.
- La herramienta de migraciones: `dotnet tool install --global dotnet-ef`.

**Antes del domingo, junta estos datos:**

- [ ] RUC, razón social, dirección fiscal y nombre comercial.
- [ ] Logo (ya lo tienes).
- [ ] Series de boleta y factura que usará el local.
- [ ] Nombres de los mozos, cajero y cocina, para crear usuarios y PIN.
- [ ] Token de Decolecta (consulta DNI/RUC).
- [ ] Token de **producción** de APISUNAT, si ya vas a facturar de verdad.
- [ ] IP de la ticketera de cocina, si es de red.
- [ ] Nombre y contraseña del WiFi del local, y acceso al router (para reservar la IP).

---

## 3. En la PC de desarrollo: generar el paquete de instalación

El paquete es un `.zip` que contiene:

- el programa ya compilado (backend y frontend);
- los scripts de la base de datos;
- los instaladores (`INSTALAR.bat`, `ACTUALIZAR.bat`…);
- esta guía.

1. **Guarda tus cambios en Git** (commit en SourceTree). Así sabes exactamente qué versión instalaste.
2. Abre **PowerShell** en `D:\restaurant-demo` y ejecuta:

   ```powershell
   cd D:\restaurant-demo
   cd src\CaraNegra.API
   dotnet restore          # descarga el paquete nuevo para el servicio de Windows
   cd ..\..
   $env:RESTAURANT_DB_CONNECTION = "Server=localhost;Port=3306;Database=restaurant_demo;User=root;Password=TU_PASSWORD;"
   powershell -ExecutionPolicy Bypass -File .\deploy\publicar.ps1
   ```

   La conexión `RESTAURANT_DB_CONNECTION` es a tu base de **desarrollo**. Solo se usa para generar el script de tablas: no se modifica nada.

3. Al terminar verás: `PAQUETE LISTO: D:\restaurant-demo\deploy\salida\RestaurantePOS-2026.10.03-1530.zip`
4. Copia ese `.zip` a la USB. Pesa unos 70–100 MB porque incluye .NET.

> **Prueba recomendada antes del domingo:** instala el paquete en otra PC o en una máquina virtual con Windows siguiendo esta guía. Es la mejor forma de llegar sin sorpresas.

---

## 4. En la PC del restaurante: preparar Windows

Haz esto **antes** de instalar.

### 4.1 Zona horaria y hora

*Configuración → Hora e idioma → Fecha y hora*:

- Zona horaria **(UTC-05:00) Bogotá, Lima, Quito**.
- "Establecer la hora automáticamente" **activado**.

### 4.2 Que nunca se suspenda

*Configuración → Sistema → Inicio/apagado y batería* (o *Energía*):

- Con corriente, **suspender: Nunca**.
- Apagar la pantalla sí está permitido.

O en PowerShell como administrador:

```powershell
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0
```

### 4.3 Windows Update sin reinicios en horario de atención

*Configuración → Windows Update → Opciones avanzadas → **Horas activas***: márcalas manualmente, por ejemplo de **9:00 a 23:59**. Así Windows no se reinicia en plena atención. Si igual se reinicia, el sistema vuelve a levantarse solo.

### 4.4 IP fija para la PC

Los celulares entran escribiendo la IP de la PC (por ejemplo `http://192.168.1.50`). Si esa IP cambia, dejan de conectarse. Hay dos formas de fijarla:

- **En el router (recomendado):** busca "Reserva DHCP" o "IP estática por MAC" y reserva una IP para esta PC.
- **En Windows:**
  1. Ve a *Configuración → Red e Internet → (Ethernet o WiFi) → Propiedades del hardware → Asignación de IP → Editar → Manual*.
  2. Usa una IP fuera del rango que reparte el router, por ejemplo `192.168.1.50`.
  3. Máscara `255.255.255.0`, puerta de enlace = IP del router (por ejemplo `192.168.1.1`) y DNS `8.8.8.8`.

Para ver la IP actual: en PowerShell, `ipconfig`, línea **Dirección IPv4**.

### 4.5 Red privada

*Configuración → Red e Internet → (tu red) → Tipo de perfil de red*: **Privada**. En una red "Pública", Windows bloquea las conexiones de los celulares. El instalador también lo revisa.

### 4.6 Nombre de la PC (opcional)

*Configuración → Sistema → Información → Cambiar el nombre de este equipo*, por ejemplo `CAJA-CUY`. Ayuda a reconocerla en la red.

---

## 5. Instalar MySQL 8.4

1. Instala el **Visual C++ Redistributable x64**, que MySQL necesita: https://aka.ms/vs/17/release/vc_redist.x64.exe
2. Descarga **MySQL Community Server 8.4 (LTS)** desde https://dev.mysql.com/downloads/mysql/ eligiendo **Windows (x86, 64-bit), MSI Installer**. En la pantalla de cuenta de Oracle puedes elegir "No thanks, just start my download".
3. Ejecuta el `.msi` con la opción **Typical**. Al terminar, deja marcada la opción de abrir **MySQL Configurator** y configura así:

   | Pantalla | Qué elegir |
   |---|---|
   | Type and Networking | Config Type: **Server Computer** · TCP/IP **3306** · **desmarca** "Open Windows Firewall ports for network access" (MySQL solo se usa dentro de esta PC) |
   | Authentication | Usar la autenticación recomendada (fuerte) |
   | Accounts and Roles | **Contraseña de root**: una contraseña fuerte. **Anótala en un lugar seguro**: el instalador del sistema la pide una vez y sirve para mantenimiento |
   | Windows Service | ✓ Configure MySQL Server as a Windows Service · Nombre: **MySQL84** · ✓ Start the MySQL Server at System Startup · Standard System Account |
   | Server File Permissions | Dejar lo propuesto |
   | Sample Databases | No |

4. Al final, **Execute** y luego **Finish**.
5. Comprueba en PowerShell que el servicio aparece en estado `Running`:

   ```powershell
   Get-Service MySQL*
   ```

**Opcional:** instala **MySQL Workbench**, desde la misma página de descargas, para revisar datos o ejecutar scripts a mano.

---

## 6. Instalar el sistema

1. Copia el `.zip` de la USB a la PC, por ejemplo a `C:\Instaladores\`, y **descomprímelo**: clic derecho → *Extraer todo*. Queda una carpeta `RestaurantePOS` con `app\`, `sql\`, `INSTALAR.bat`, etc.
2. **Clic derecho sobre `INSTALAR.bat` → Ejecutar como administrador.**
3. El instalador hace estas preguntas:

   | Pregunta | Qué responder |
   |---|---|
   | Contraseña de root | La de MySQL (paso 5) |
   | ¿Cargar la carta de La Casa del Cuy? | **S**. Carga las 9 categorías y 65 productos |
   | Token de Decolecta | Pégalo, o Enter para dejarlo para después |
   | ¿Emitir comprobantes REALES con APISUNAT desde ya? | **S** solo si ya tienes el token de **producción** y autorizaste a APISUNAT como PSE en SUNAT. Si no, **N**: los comprobantes salen como "DEMO – SIN VALIDEZ TRIBUTARIA" |
   | ¿Marcar la red como PRIVADA? | Si aparece, **S** |

4. El instalador hace todo esto solo:

   | Paso | Detalle |
   |---|---|
   | Revisa requisitos | Que MySQL esté instalado y que el puerto 80 esté libre |
   | Copia el programa | A `C:\RestaurantePOS\app` |
   | Prepara la base de datos | Crea la base **`restaurante`** y el usuario **`restaurante`**, con una contraseña generada al azar que nadie necesita recordar. Crea las tablas y carga la carta |
   | Genera la configuración | `C:\RestaurantePOS\app\appsettings.Production.json`, con la conexión, una clave JWT aleatoria y el puerto |
   | Crea el servicio `RestaurantePOS` | Inicio automático (retrasado) · cuenta de servicio propia · depende de MySQL · se reinicia solo si falla |
   | Abre el firewall | El puerto 80, solo para la red privada |
   | Programa el respaldo | Respaldo diario a las 03:00 (si la PC estaba apagada, se hace al encenderla) |
   | Arranca y verifica | Inicia el servicio, comprueba que responda y hace un primer respaldo de prueba |

5. Al final verás algo así:

   ```
   INSTALACIÓN TERMINADA
     En esta PC:          http://localhost
     Celulares / tablets: http://192.168.1.50

     Usuario ADMIN inicial:  admin
     Contraseña temporal:    Xk3...
   ```

   **Anota la contraseña temporal del admin.** Solo se muestra esta vez y la cambiarás al entrar.

**Opciones del instalador** (desde PowerShell como administrador, en la carpeta del paquete):

```powershell
# Otro puerto si el 80 está ocupado (los celulares entrarán con http://IP:8080):
powershell -ExecutionPolicy Bypass -File .\instalar.ps1 -Puerto 8080
# Otra hora para el respaldo diario:
powershell -ExecutionPolicy Bypass -File .\instalar.ps1 -HoraRespaldo 23:45
```

> Si el instalador se detiene a la mitad, lee el mensaje en rojo y corrige la causa (sección 13). Luego ejecuta **DESINSTALAR.bat** y vuelve a ejecutar **INSTALAR.bat**: la base de datos ya creada se reutiliza.

---

## 7. Opcional: llevar la configuración de la demo

Usa esta opción si en la demo ya configuraste salones, mesas, carta con precios, usuarios, datos de la empresa y logo, y quieres conservarlos.

Se copian a producción, pero **sin** los pedidos, pagos, comprobantes, jornadas ni clientes de prueba.

**En la PC de desarrollo**, primero asegúrate de que la demo tiene **todas las migraciones aplicadas** (`dotnet ef database update`), así su estructura es la misma que la del paquete. Luego exporta la base de la demo:

```powershell
& "C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe" -u root -p --single-transaction --no-tablespaces --default-character-set=utf8mb4 --result-file=D:\demo-config.sql restaurant_demo
```

Ajusta la ruta si tu MySQL es 8.0 (`MySQL Server 8.0`). Copia `D:\demo-config.sql` a la USB.

**En la PC del restaurante**, después de la sección 6, en **PowerShell como administrador**:

```powershell
Stop-Service RestaurantePOS
cd "C:\Program Files\MySQL\MySQL Server 8.4\bin"
# 1) Importar la demo sobre la base de producción (pide la contraseña de root)
cmd /c "mysql.exe -u root -p --default-character-set=utf8mb4 restaurante < E:\demo-config.sql"
# 2) Borrar los datos de prueba, conservando la configuración
cmd /c "mysql.exe -u root -p --default-character-set=utf8mb4 restaurante < C:\Instaladores\RestaurantePOS\sql\opcional-limpiar-datos-de-prueba.sql"
Start-Service RestaurantePOS
```

En ese ejemplo, `E:` es la USB: cámbialo por la letra que corresponda. Luego **entra con tus usuarios de la demo**: el `admin` y la contraseña temporal del paso 6 dejan de valer, porque se reemplazaron por los de la demo.

> Los correlativos de boletas y facturas vuelven a empezar en 1. Si el local ya emitía comprobantes electrónicos con esas series, usa series nuevas en *Datos de la empresa*.

---

## 8. Primer ingreso y configuración inicial

En la PC del restaurante abre **Chrome** en `http://localhost` y entra con `admin` y la contraseña temporal.

1. **Cambia la contraseña del admin**: ícono de tu perfil → Cambiar contraseña.
2. **Admin → Datos de la empresa:**
   - RUC (el botón SUNAT completa la razón social y la dirección);
   - nombre comercial, teléfono y **logo**;
   - **series** de boleta y factura;
   - **% IGV** (18, o 10.5 si es MYPE restaurante acogida);
   - mensaje al pie.
3. **Admin → Salones y Mesas:** crea las áreas (Terraza, Jardín…) y sus mesas.
4. **Admin → Carta:** revisa precios y disponibilidad. Si cargaste la carta, ya está.
5. **Admin → Usuarios:**
   - crea el **cajero** (rol CAJERO), los **mozos** (MOZO) y **cocina** (COCINA);
   - a mozos y cocina asígnales un **PIN** con el ícono de cuadrícula.
6. **Admin → Dispositivos con PIN:** en **cada** celular y tablet del local, entra como admin, abre esta pantalla, pulsa **Registrar este dispositivo** y cierra sesión. Desde ahí, los mozos entran con PIN.
7. **Ticketera de cocina** (si es de red): edita la configuración (sección 10.3) con `"Habilitada": true` y su IP.
8. **Prueba completa:**
   1. abre la jornada;
   2. toma un pedido desde un celular;
   3. revisa que llegue a cocina;
   4. cóbralo, emite una boleta e imprímela;
   5. cierra la jornada con su arqueo.

   Si quieres que la prueba no quede en el sistema, ejecuta `opcional-limpiar-datos-de-prueba.sql` como en la sección 7 (solo el paso 2) **antes de abrir el primer día real**.

---

## 9. Celulares, tablet de cocina, caja e impresoras

**Celulares y tablet:**

1. Conéctalos al **WiFi del local**: la misma red que la PC.
2. Abre Chrome (Android) o Safari (iPhone) en `http://IP-DE-LA-PC`, por ejemplo `http://192.168.1.50`.
3. Menú → **Agregar a la pantalla de inicio**, para tener un ícono como de app.
4. Regístralos como dispositivos con PIN (sección 8, paso 6).
   - En la **tablet compartida** de mozos, elige "cerrar sesión a los 5 min sin uso".
   - En el celular personal de un mozo, elige "Nunca".

**Cocina:** la tablet o TV entra con el usuario de cocina (PIN) y queda en la pantalla de cocina. Usa el botón ⛶ para la pantalla completa y 🔊 para el sonido.

**Caja (la misma PC):** Chrome en `http://localhost`. Las boletas y los recibos se imprimen con la impresora predeterminada de Windows:

- Pon la **ticketera de 80 mm** como **impresora predeterminada**, con su driver instalado y papel de 80 mm.
- En el diálogo de impresión de Chrome, la primera vez, configura: márgenes **Ninguno**, escala **100 %** y sin encabezados ni pies de página.
- *(Opcional)* Para imprimir **sin el diálogo** de impresión, crea un acceso directo de Chrome con este destino y úsalo solo para la caja:

  ```
  "C:\Program Files\Google\Chrome\Application\chrome.exe" --kiosk-printing http://localhost
  ```

> Por seguridad del navegador, en una red local con `http://` la app no se "instala" como PWA completa. El acceso directo de la pantalla de inicio funciona igual para el día a día.

---

## 10. Administrar el servicio

### 10.1 Comandos básicos (PowerShell como administrador)

```powershell
Get-Service RestaurantePOS          # ver estado (Running = funcionando)
Restart-Service RestaurantePOS      # reiniciar (p. ej. tras cambiar la configuración)
Stop-Service RestaurantePOS         # detener
Start-Service RestaurantePOS        # iniciar
```

También desde la ventana **Servicios** (`Win + R` → `services.msc` → "Restaurante POS"): clic derecho → Iniciar / Detener / Reiniciar.

**¿Está vivo?** Abre `http://localhost/health`: debe decir `Healthy`.

### 10.2 Dónde están las cosas

| Qué | Dónde |
|---|---|
| Programa | `C:\RestaurantePOS\app` |
| **Configuración** | `C:\RestaurantePOS\app\appsettings.Production.json` |
| **Registros (logs)** | `C:\RestaurantePOS\app\logs\caranegra-AAAAMMDD.log` (uno por día) |
| Respaldos | `C:\RestaurantePOS\respaldos` |
| Herramientas | `C:\RestaurantePOS\herramientas` (respaldar, restaurar, desinstalar, esta guía) |
| Versiones anteriores | `C:\RestaurantePOS\versiones` (las crea `ACTUALIZAR.bat`) |
| Errores de arranque | Visor de eventos → Registros de Windows → **Aplicación** (origen `CaraNegra.API`, `.NET Runtime` o `Service Control Manager` en *Sistema*) |

### 10.3 Cambiar la configuración

1. Abre el **Bloc de notas como administrador** (clic derecho → Ejecutar como administrador).
2. Abre `C:\RestaurantePOS\app\appsettings.Production.json`. Solo los administradores pueden leerlo, porque tiene claves.
3. Cambia lo necesario, respetando comillas y comas:

   | Qué | Claves |
   |---|---|
   | Consulta DNI/RUC | `ConsultaDocumentos.Token` |
   | Facturación real | `FacturacionElectronica.Proveedor` = `"ApiSunat"`, `Ambiente` = `"Produccion"`, `Token` = token de producción de APISUNAT |
   | Ticketera de cocina | `ImpresoraCocina.Habilitada` = `true`, `Ip` = `"192.168.1.60"` |
   | Puerto | `Kestrel.Endpoints.Http.Url` = `"http://0.0.0.0:8080"`. Si lo cambias, crea también la regla de firewall (sección 13) |

4. Guarda y ejecuta `Restart-Service RestaurantePOS`.
5. Si el servicio no inicia después de editar, casi siempre es un error de formato (una coma o comilla de más). Revisa el log o el Visor de eventos.

> **No guardes claves en otro lado** (ni en Git, ni en WhatsApp). En producción **no** se usan los `user-secrets` de desarrollo: todo va en este archivo.

---

## 11. Respaldos y restauración

**Automático:** todos los días a las 03:00 se crea `C:\RestaurantePOS\respaldos\restaurante_AAAA-MM-DD_HHMM.sql.zip`. Se conservan los últimos **30 días**. El historial queda en `respaldos\respaldo.log`.

**Respaldo manual:** clic derecho en `C:\RestaurantePOS\herramientas\RESPALDAR-AHORA.bat` → Ejecutar como administrador. Conviene hacerlo antes de cambios grandes.

**Copia fuera de la PC (muy recomendado).** Un respaldo dentro de la misma PC no sirve si se malogra el disco o la roban.

1. Elige una carpeta externa: una USB o disco que quede conectado, o una carpeta sincronizada con **Google Drive / OneDrive** (por ejemplo `C:\Users\Caja\Google Drive\Respaldos`).
2. Crea el archivo `C:\RestaurantePOS\respaldos\copia-extra.txt` con esa ruta en la **primera línea**.
3. Desde ahí, cada respaldo se copia también a esa carpeta.

**Restaurar** (vuelve la base al estado de un respaldo y **reemplaza los datos actuales**):

```powershell
cd C:\RestaurantePOS\herramientas
powershell -ExecutionPolicy Bypass -File .\restaurar-respaldo.ps1 -Archivo "C:\RestaurantePOS\respaldos\restaurante_2026-10-04_0300.sql.zip"
```

Antes de restaurar, el script hace un respaldo del estado actual (por si acaso), detiene el servicio, restaura y lo vuelve a iniciar.

> **Prueba la restauración una vez**, en otra PC o en un momento sin atención. Un respaldo que nunca se probó no es un respaldo.

---

## 12. Instalar una versión nueva

1. En la PC de desarrollo: commit y luego `deploy\publicar.ps1` (sección 3).
2. Lleva el `.zip` nuevo y descomprímelo en otra carpeta, por ejemplo `C:\Instaladores\RestaurantePOS-2026.10.20-2200`.
3. **Con el local cerrado**, haz clic derecho en **`ACTUALIZAR.bat` → Ejecutar como administrador**. El script:
   1. respalda la base de datos;
   2. detiene el servicio y guarda una copia de la versión actual en `C:\RestaurantePOS\versiones`;
   3. copia la versión nueva, **conservando la configuración y los logs**;
   4. aplica los cambios de tablas, solo los que falten;
   5. inicia el servicio y comprueba que responda;
   6. **si la versión nueva no arranca**, ofrece volver a la anterior.
4. En los celulares y tablets, **recarga la página**.

---

## 13. Solución de problemas

| Síntoma | Causa probable | Solución |
|---|---|---|
| En la PC funciona `http://localhost` pero **los celulares no cargan** | Red marcada como Pública, firewall, o el celular está en otra red (datos móviles o WiFi de invitados) | Red **Privada** (sección 4.5); celular en el **mismo WiFi**; comprueba que exista la regla con `Get-NetFirewallRule -Name RestaurantePOS-HTTP`; prueba `http://IP/health` desde el celular |
| Los celulares funcionaban y **de un día a otro ya no** | La IP de la PC cambió | Revisa con `ipconfig` y **fija la IP** (sección 4.4) |
| El instalador dice **"El puerto 80 ya está en uso"** | IIS, Skype antiguo, XAMPP/Apache u otro programa | Desactiva el "Servicio de publicación World Wide Web" (IIS) en `services.msc`, o instala con `-Puerto 8080` |
| **El servicio no inicia** o se detiene solo | MySQL detenido, configuración con error de formato, o clave de BD incorrecta | `Get-Service MySQL84` (debe estar Running); revisa el último archivo de `app\logs` y el Visor de eventos; valida el JSON (sección 10.3) |
| Error **1053** al iniciar el servicio | Tardó demasiado al arrancar (MySQL aún no listo) | Windows lo reintenta solo a los 10, 30 y 60 s. Si persiste, revisa los logs |
| "**El día aún no se ha abierto**" | No hay jornada abierta | El admin o el cajero abre la jornada (Caja → Jornada) |
| No sale la **comanda en la ticketera de cocina** | `ImpresoraCocina` deshabilitada o IP incorrecta | Configura la IP (sección 10.3); desde la PC prueba `Test-NetConnection 192.168.1.60 -Port 9100` |
| La **boleta sale cortada o con márgenes** | Configuración de impresión de Chrome | Márgenes "Ninguno", escala 100 %, papel de 80 mm, ticketera como predeterminada |
| No autocompleta el **nombre por DNI/RUC** | Falta el token de Decolecta o se acabó el saldo | `ConsultaDocumentos.Token` y reiniciar; mientras tanto, se escribe a mano |
| Comprobantes "**Por enviar**" | Sin internet o token de APISUNAT incorrecto | Se reintentan solos cada 2 min; revisa el token y el ambiente (Produccion) |
| **Se perdió la contraseña del único ADMIN** | — | Ver el procedimiento debajo de esta tabla |
| Abrí el puerto 8080 pero los celulares no entran | Falta la regla de firewall para el puerto nuevo | Ver el comando debajo de esta tabla |

**Recuperar el acceso si se perdió la contraseña del único ADMIN.** En MySQL Workbench, con root, ejecuta esto (pasa el admin actual a cajero desactivado):

```sql
UPDATE restaurante.Usuarios
SET NombreUsuario = CONCAT(NombreUsuario, '_old'),
    EsVerificado = 0,
    RolId = (SELECT RolId FROM restaurante.Roles WHERE Nombre = 'CAJERO')
WHERE RolId = (SELECT RolId FROM restaurante.Roles WHERE Nombre = 'ADMIN');
```

Luego ejecuta `Restart-Service RestaurantePOS`. Se crea un usuario `admin` nuevo y su contraseña temporal aparece en el log del día (busca "Contraseña temporal").

**Abrir en el firewall un puerto distinto al 80** (ejemplo con el 8080):

```powershell
New-NetFirewallRule -Name RestaurantePOS-HTTP-8080 -DisplayName "Restaurante POS (8080)" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow -Profile Private,Domain
```

**Ver los últimos errores del log:**

```powershell
Get-ChildItem C:\RestaurantePOS\app\logs | Sort-Object LastWriteTime | Select-Object -Last 1 | Get-Content -Tail 80
```

---

## 14. Lista de verificación del día de la instalación

**Antes de ir**
- [ ] Commit de la versión final y paquete generado con `publicar.ps1`. **Probado en otra PC.**
- [ ] USB con: el `.zip`, el instalador de MySQL 8.4, `vc_redist.x64.exe` (por si no hay buena conexión) y, si aplica, `demo-config.sql`.
- [ ] Datos de la empresa, tokens, logo, lista de personal, series.

**En el local**
- [ ] Zona horaria, energía (no suspender), horas activas de Windows Update (sección 4).
- [ ] IP fija para la PC y red **Privada**.
- [ ] MySQL 8.4 instalado (servicio `MySQL84` en Running). **Contraseña de root anotada.**
- [ ] `INSTALAR.bat` como administrador → "INSTALACIÓN TERMINADA". **Contraseña temporal del admin anotada.**
- [ ] (Opcional) Configuración de la demo importada y limpiada (sección 7).
- [ ] Contraseña del admin cambiada; datos de la empresa y logo; salones y mesas; carta revisada.
- [ ] Usuarios (cajero, mozos, cocina) con PIN; celulares y tablet **registrados**.
- [ ] Ticketera de caja como predeterminada; ticketera de cocina configurada.
- [ ] Prueba completa: abrir jornada → pedido → cocina → cobro → boleta → cierre de jornada.
- [ ] **Reiniciar la PC y comprobar** que, sin iniciar sesión, los celulares vuelven a entrar. El servicio tiene inicio *retrasado*: espera unos 2–3 minutos después de encender.
- [ ] Respaldo manual con `RESPALDAR-AHORA.bat`; copia extra configurada (`copia-extra.txt`).
- [ ] (Si se hicieron pruebas) datos de prueba limpiados antes del primer día real.

---

## 15. Anexos

### 15.1 Estructura de carpetas

```
C:\RestaurantePOS\
├─ app\                         programa (no tocar, salvo la configuración)
│  ├─ CaraNegra.API.exe
│  ├─ appsettings.Production.json   ← configuración (con claves)
│  ├─ wwwroot\                  pantallas (React compilado)
│  └─ logs\                     registros diarios
├─ herramientas\                respaldar, restaurar, desinstalar, esta guía
├─ respaldos\                   respaldos diarios (.sql.zip) + respaldo.log
└─ versiones\                   copias de versiones anteriores (actualizaciones)
```

### 15.2 Qué se instaló en Windows

| Elemento | Nombre |
|---|---|
| Servicio del sistema | `RestaurantePOS` ("Restaurante POS"), inicio automático retrasado, cuenta `NT SERVICE\RestaurantePOS` |
| Servicio de base de datos | `MySQL84` |
| Base de datos y usuario | `restaurante` / `restaurante` (la clave está en la configuración) |
| Regla de firewall | `RestaurantePOS-HTTP` (TCP 80, red privada) |
| Tarea programada | `RestaurantePOS - Respaldo diario` (03:00, como SYSTEM) |

**Desinstalar:** `C:\RestaurantePOS\herramientas\DESINSTALAR.bat` (como administrador). Quita el servicio, la regla y la tarea; **no** borra la base ni los respaldos.

### 15.3 Acceso remoto (opcional)

Para que el dueño vea el sistema desde su casa:

- **Tailscale** (gratis para uso personal). Instálalo en la PC del local y en el celular o laptop del dueño, con la misma cuenta. Corre como servicio y el sistema queda accesible en `http://nombre-de-la-pc` (o su IP 100.x.x.x) solo para tus dispositivos. Es la opción más segura.
- **Escritorio remoto de Chrome**, para manejar la PC a distancia (reiniciar el servicio, ver logs): https://remotedesktop.google.com/access

> No abras puertos del router hacia internet ni uses el túnel de la demo (`trycloudflare`) en producción: dejaría el sistema expuesto públicamente.

### 15.4 Detalles técnicos (para quien mantenga el sistema)

- **El backend corre como servicio** con `Microsoft.Extensions.Hosting.WindowsServices` (`UseWindowsService`). Al ejecutarse como servicio, el directorio de trabajo pasa a ser la carpeta del `.exe`, para que `logs/` y la configuración se resuelvan bien.
- **Publicación:** `dotnet publish -c Release -r win-x64 --self-contained`. El frontend se compila con `VITE_API_URL=/api/v1` y se copia a `wwwroot`. El backend lo sirve:
  - `UseDefaultFiles` + `UseStaticFiles`;
  - `/assets/*` con caché inmutable; `index.html`, `sw.js` y `manifest` sin caché;
  - las rutas que no son `/api` ni `/hubs` devuelven `index.html` (para React Router).
- **Arranque:** antes de sembrar datos se espera a MySQL ~20 s (`EsperaBaseDeDatos`). Además, el servicio depende de `MySQL84` y tiene reinicio automático configurado con `sc.exe failure`.
- **Ambiente:** `ASPNETCORE_ENVIRONMENT` no se define, así que es **Production** por defecto y se cargan `appsettings.json` + `appsettings.Production.json`. Los `user-secrets` solo se usan en Development.
- **Tablas:**
  - base nueva: `sql/01-estructura-base-nueva.sql` (`dotnet ef migrations script`);
  - actualizaciones: `sql/01-estructura.sql` (`--idempotent`), que solo aplica las migraciones que falten.
- La alternativa con Docker sigue documentada en `DESPLIEGUE.md`, pero **no** se usa en esta instalación.
