-- =====================================================================
--  exportar-fotos.sql - Genera un script con las fotos de los platos
--  para llevarlas a OTRA base de datos (por ejemplo, de desarrollo a la
--  PC del restaurante).
--
--  Las fotos se emparejan por NOMBRE del producto (no por Id), porque los
--  Id no tienen por que coincidir entre una base y otra.
--
--  Uso (en CMD, no en PowerShell, para que el archivo salga en UTF-8):
--    mysql -u root -p --default-character-set=utf8mb4 -N -r -B restaurant_demo < exportar-fotos.sql > D:\fotos-carta.sql
--
--  (fuera del repositorio: no se sube a Git). El archivo resultante se ejecuta en la base de destino.
--  Si el plato ya tiene foto alli, se reemplaza. Los platos cuyo nombre no
--  exista en el destino simplemente se ignoran.
-- =====================================================================
SELECT CONCAT(
    'INSERT INTO ProductoImagenes (ProductoId, Datos, TipoContenido, ActualizadoEn) ',
    'SELECT ProductoId, 0x', HEX(i.Datos), ', ', QUOTE(i.TipoContenido), ', UTC_TIMESTAMP() ',
    'FROM Productos WHERE Nombre = ', QUOTE(p.Nombre), ' ',
    'ON DUPLICATE KEY UPDATE Datos = VALUES(Datos), TipoContenido = VALUES(TipoContenido), ActualizadoEn = VALUES(ActualizadoEn); ',
    'UPDATE Productos SET ImagenVersion = ImagenVersion + 1 WHERE Nombre = ', QUOTE(p.Nombre), ';'
)
FROM ProductoImagenes i
JOIN Productos p ON p.ProductoId = i.ProductoId
ORDER BY p.Nombre;
