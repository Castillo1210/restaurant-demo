-- =====================================================================
--  OPCIONAL - Limpiar los datos de PRUEBA conservando la configuración
--
--  Úsalo SOLO si pasaste a producción una copia de la base de la demo
--  (Opción B de INSTALACION_WINDOWS.md, sección 5). Borra todo lo que es
--  movimiento del día a día:
--     pedidos, pagos, comprobantes, jornadas/cierres, movimientos de caja,
--     movimientos de inventario, clientes consultados y dispositivos con PIN.
--  CONSERVA la configuración:
--     datos de la empresa y logo, salones, mesas, carta (categorías y
--     productos), cremas, descuentos, métodos de pago, artículos de
--     inventario, usuarios y roles.
--
--  Antes de ejecutarlo: detén el servicio (Stop-Service RestaurantePOS).
--  Ejecutar sobre la base de PRODUCCIÓN (por defecto: restaurante).
-- =====================================================================

SET FOREIGN_KEY_CHECKS = 0;

DELETE FROM DetallesDescuento;
DELETE FROM Pagos;
DELETE FROM Comprobantes;
DELETE FROM DetallesPedido;
DELETE FROM Pedidos;
DELETE FROM MovimientosCaja;
DELETE FROM JornadaArqueos;
DELETE FROM Jornadas;
DELETE FROM MovimientosArticulo;
DELETE FROM Clientes;
DELETE FROM DispositivosAutorizados;

-- Mesas ocupadas por pedidos de prueba -> libres (0 = Disponible, 1 = Ocupada).
UPDATE Mesas SET Estado = 0 WHERE Estado = 1;
-- PIN sin bloqueos.
UPDATE Usuarios SET PinIntentosFallidos = 0, PinBloqueadoHasta = NULL;

SET FOREIGN_KEY_CHECKS = 1;

-- Los correlativos de boletas y facturas vuelven a empezar en 1 (se calculan con los
-- comprobantes existentes). Si en producción vas a usar series nuevas (p. ej. B002/F002),
-- cámbialas en Admin > Datos de la empresa.

SELECT 'Datos de prueba eliminados' AS Resultado;
