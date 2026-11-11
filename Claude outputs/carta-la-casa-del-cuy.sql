-- =====================================================================
--  Carta de "La Casa del Cuy"  ->  categorías y productos
--  Fuente: fotos de la carta (27/09/2026). 9 categorías, 65 productos.
--
--  Base de datos: restaurant_demo (NUNCA la de Cara Negra).
--  Ejecutar en MySQL Workbench (File > Open SQL Script > rayo "Execute")
--  o por consola:
--      mysql -u root -p restaurant_demo < scripts\carta-la-casa-del-cuy.sql
--
--  Es IDEMPOTENTE: si se ejecuta dos veces no duplica nada (compara por nombre).
--  Si un producto ya existe no se toca (los precios se cambian en Admin > Carta).
--  Al final hay un bloque OPCIONAL (comentado) para ocultar los productos de
--  prueba que no están en esta carta.
--
--  Notas:
--   - Las variantes sin diferencia de precio (Inca Kola / Coca Cola, clásico /
--     maracuyá, pollo / chancho...) van en la descripción: el mozo las indica
--     en la nota del pedido.
--   - Los licores con precio distinto por vaso y por jarra son 2 productos.
-- =====================================================================

USE restaurant_demo;
SET NAMES utf8mb4;

START TRANSACTION;

-- ---------------------------------------------------------------------
--  Categorías
-- ---------------------------------------------------------------------
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Entradas', 'Entradas y piqueos fríos', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Entradas');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Platos de fondo', 'Cuy, chicharrones y platos tacneños', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Platos de fondo');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Parrilladas', 'Parrilladas personales', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Parrilladas');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Fuentes de parrillada', 'Parrilladas para compartir', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Fuentes de parrillada');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Fuentes de chicharrón', 'Chicharrón para compartir', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Fuentes de chicharrón');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Guarniciones', 'Porciones adicionales', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Guarniciones');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Bebidas', 'Gaseosas, aguas, infusiones y jarras', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Bebidas');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Vinos y cervezas', 'Vinos y cervezas', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Vinos y cervezas');
INSERT INTO Categorias (Nombre, Descripcion, EstaActivo, CreadoEn)
SELECT 'Licores', 'Cócteles con pisco, por vaso o jarra', 1, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Categorias WHERE Nombre = 'Licores');

-- ---------------------------------------------------------------------
--  Entradas (5)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Entradas' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Solterito de Queso', NULL, 13.00, 1, 'Entrada', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Solterito de Queso' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Choclo con Queso', NULL, 13.00, 1, 'Entrada', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Choclo con Queso' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Palta Rellena', NULL, 17.00, 1, 'Entrada', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Palta Rellena' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Papa a la Huancaína', NULL, 15.00, 1, 'Entrada', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Papa a la Huancaína' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Salpicón de Pollo', NULL, 17.00, 1, 'Entrada', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Salpicón de Pollo' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Platos de fondo (12)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Platos de fondo' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cuy Chactado', NULL, 55.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cuy Chactado' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cuy a la Parrilla', NULL, 57.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cuy a la Parrilla' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Combo Cuy', '1 cuy, 3 presas de pollo y 3 de chancho', 100.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Combo Cuy' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Chicharrón Mixto (5 presas)', 'Pollo o chancho', 38.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Chicharrón Mixto (5 presas)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Chicharrón de Chancho', NULL, 40.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Chicharrón de Chancho' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Picante a la Tacneña', NULL, 25.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Picante a la Tacneña' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cazuela de Gallina', NULL, 25.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cazuela de Gallina' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT '1/2 Cazuela de Gallina', NULL, 18.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = '1/2 Cazuela de Gallina' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Arroz con Pato', NULL, 30.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Arroz con Pato' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Combinado', 'Arroz con pato + picante', 36.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Combinado' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Triple Tacneño', 'Arroz con pato + picante + 2 chicharrón', 45.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Triple Tacneño' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Milanesa de Pollo', NULL, 32.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Milanesa de Pollo' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Parrilladas (7)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Parrilladas' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrillada Mixta I', 'Pollo + chancho + chorizo', 45.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrillada Mixta I' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrillada Mixta II', 'Pollo + cordero + chorizo', 45.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrillada Mixta II' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrillada Mixta III', 'Chancho + cordero + chorizo', 45.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrillada Mixta III' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrillada de Cordero', '2 presas', 45.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrillada de Cordero' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrillada de Lechón', '2 presas', 35.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrillada de Lechón' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrilla Personal', 'Pollo o cordero + chorizo', 30.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrilla Personal' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Pollo a la Parrilla', NULL, 25.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Pollo a la Parrilla' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Fuentes de parrillada (3)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Fuentes de parrillada' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrillada Doble', '2 pollo + 2 lechón + 2 cordero + 2 chorizo', 120.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrillada Doble' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Parrillada Familiar', '3 pollo + 3 lechón + 3 cordero + 3 chorizo', 165.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Parrillada Familiar' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cuy Parrillero', '1 cuy + 1 pollo + 1 lechón + 1 cordero + 1 chorizo', 110.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cuy Parrillero' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Fuentes de chicharrón (4)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Fuentes de chicharrón' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Fuente de Chicharrón de Pollo, Chancho o Mixto (12 presas)', 'Indicar pollo, chancho o mixto', 90.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Fuente de Chicharrón de Pollo, Chancho o Mixto (12 presas)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Fuente de Chicharrón de Pollo, Chancho o Mixto (18 presas)', 'Indicar pollo, chancho o mixto', 135.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Fuente de Chicharrón de Pollo, Chancho o Mixto (18 presas)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Fuente de Chicharrón de Chancho (12 presas)', NULL, 95.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Fuente de Chicharrón de Chancho (12 presas)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Fuente de Chicharrón de Chancho (18 presas)', NULL, 140.00, 1, 'Plato', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Fuente de Chicharrón de Chancho (18 presas)' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Guarniciones (8)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Guarniciones' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Papa Frita', NULL, 15.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Papa Frita' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Papa Dorada', NULL, 15.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Papa Dorada' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Papa Cocida', NULL, 10.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Papa Cocida' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Canchita', 'Trozos de chancho y papa', 15.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Canchita' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Chorizo', '3 unidades', 10.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Chorizo' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Arroz Verde', NULL, 15.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Arroz Verde' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Choclo', '2 unidades', 10.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Choclo' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Porción de Ensalada o Sarsa', 'Cebolla, lechuga y verdura', 5.00, 1, 'Acompañamiento', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Porción de Ensalada o Sarsa' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Bebidas (12)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Bebidas' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Gaseosa 2 1/2 Litros', 'Inca Kola o Coca Cola', 15.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Gaseosa 2 1/2 Litros' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Gaseosa 1 Litro', 'Inca Kola o Coca Cola', 9.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Gaseosa 1 Litro' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Gaseosa 1/2 Litro', 'Inca Kola o Coca Cola', 5.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Gaseosa 1/2 Litro' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Gaseosa Personal Vidrio 296 ml', NULL, 3.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Gaseosa Personal Vidrio 296 ml' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Agua 2 1/2 Litros', 'Cielo o San Luis', 7.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Agua 2 1/2 Litros' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Agua 1/2 Litro', NULL, 3.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Agua 1/2 Litro' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Infusión', 'Té o café', 3.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Infusión' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Jarra de Limonada (1 L)', NULL, 13.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Jarra de Limonada (1 L)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Jarra de Limonada Frozen (1 L)', NULL, 15.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Jarra de Limonada Frozen (1 L)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Jarra de Maracuyá (1 L)', NULL, 15.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Jarra de Maracuyá (1 L)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Jarra de Maracuyá Frozen (1 L)', NULL, 15.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Jarra de Maracuyá Frozen (1 L)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Jarra de Chicha Morada (1 L)', NULL, 13.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Jarra de Chicha Morada (1 L)' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Vinos y cervezas (6)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Vinos y cervezas' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Vino Tinto', 'Seco, semiseco o borgoña', 20.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Vino Tinto' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Vino Tinto 1/2 Litro', 'Seco, semiseco o borgoña', 12.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Vino Tinto 1/2 Litro' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cerveza Cusqueña', 'Blanca, malta o trigo', 13.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cerveza Cusqueña' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cerveza Pilsen', NULL, 11.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cerveza Pilsen' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cerveza Cristal', NULL, 11.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cerveza Cristal' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Cerveza Corona', NULL, 9.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Cerveza Corona' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  Licores (8)
-- ---------------------------------------------------------------------
SET @cat = (SELECT CategoriaId FROM Categorias WHERE Nombre = 'Licores' ORDER BY CategoriaId LIMIT 1);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Pisco Sour / Tacna Sour (vaso)', 'Clásico o maracuyá', 12.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Pisco Sour / Tacna Sour (vaso)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Pisco Sour / Tacna Sour (jarra)', 'Clásico o maracuyá', 30.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Pisco Sour / Tacna Sour (jarra)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Chilcano (vaso)', 'Limón o maracuyá', 12.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Chilcano (vaso)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Chilcano (jarra)', 'Limón o maracuyá', 30.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Chilcano (jarra)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Piscojito (vaso)', 'Limón o maracuyá', 12.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Piscojito (vaso)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Piscojito (jarra)', 'Limón o maracuyá', 30.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Piscojito (jarra)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Mojito (vaso)', 'Limón o maracuyá', 15.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Mojito (vaso)' AND CategoriaId = @cat);
INSERT INTO Productos (Nombre, Descripcion, Precio, EstaDisponible, Tipo, CategoriaId, CreadoEn)
SELECT 'Mojito (jarra)', 'Limón o maracuyá', 30.00, 1, 'Bebida', @cat, UTC_TIMESTAMP(6) FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Nombre = 'Mojito (jarra)' AND CategoriaId = @cat);

-- ---------------------------------------------------------------------
--  OPCIONAL: ocultar los productos y categorías de prueba que NO son de esta
--  carta (no se borran: pueden tener pedidos). Quitar los "-- " para usarlo.
-- ---------------------------------------------------------------------
-- SET SQL_SAFE_UPDATES = 0;   -- MySQL Workbench bloquea UPDATE sin clave si no se desactiva
-- UPDATE Productos SET EstaDisponible = 0, ActualizadoEn = UTC_TIMESTAMP(6)
-- WHERE Nombre NOT IN (
--     'Solterito de Queso',
--     'Choclo con Queso',
--     'Palta Rellena',
--     'Papa a la Huancaína',
--     'Salpicón de Pollo',
--     'Cuy Chactado',
--     'Cuy a la Parrilla',
--     'Combo Cuy',
--     'Chicharrón Mixto (5 presas)',
--     'Chicharrón de Chancho',
--     'Picante a la Tacneña',
--     'Cazuela de Gallina',
--     '1/2 Cazuela de Gallina',
--     'Arroz con Pato',
--     'Combinado',
--     'Triple Tacneño',
--     'Milanesa de Pollo',
--     'Parrillada Mixta I',
--     'Parrillada Mixta II',
--     'Parrillada Mixta III',
--     'Parrillada de Cordero',
--     'Parrillada de Lechón',
--     'Parrilla Personal',
--     'Pollo a la Parrilla',
--     'Parrillada Doble',
--     'Parrillada Familiar',
--     'Cuy Parrillero',
--     'Fuente de Chicharrón de Pollo, Chancho o Mixto (12 presas)',
--     'Fuente de Chicharrón de Pollo, Chancho o Mixto (18 presas)',
--     'Fuente de Chicharrón de Chancho (12 presas)',
--     'Fuente de Chicharrón de Chancho (18 presas)',
--     'Porción de Papa Frita',
--     'Porción de Papa Dorada',
--     'Porción de Papa Cocida',
--     'Porción de Canchita',
--     'Porción de Chorizo',
--     'Porción de Arroz Verde',
--     'Porción de Choclo',
--     'Porción de Ensalada o Sarsa',
--     'Gaseosa 2 1/2 Litros',
--     'Gaseosa 1 Litro',
--     'Gaseosa 1/2 Litro',
--     'Gaseosa Personal Vidrio 296 ml',
--     'Agua 2 1/2 Litros',
--     'Agua 1/2 Litro',
--     'Infusión',
--     'Jarra de Limonada (1 L)',
--     'Jarra de Limonada Frozen (1 L)',
--     'Jarra de Maracuyá (1 L)',
--     'Jarra de Maracuyá Frozen (1 L)',
--     'Jarra de Chicha Morada (1 L)',
--     'Vino Tinto',
--     'Vino Tinto 1/2 Litro',
--     'Cerveza Cusqueña',
--     'Cerveza Pilsen',
--     'Cerveza Cristal',
--     'Cerveza Corona',
--     'Pisco Sour / Tacna Sour (vaso)',
--     'Pisco Sour / Tacna Sour (jarra)',
--     'Chilcano (vaso)',
--     'Chilcano (jarra)',
--     'Piscojito (vaso)',
--     'Piscojito (jarra)',
--     'Mojito (vaso)',
--     'Mojito (jarra)'
-- );
-- UPDATE Categorias SET EstaActivo = 0, ActualizadoEn = UTC_TIMESTAMP(6)
-- WHERE Nombre NOT IN ('Entradas', 'Platos de fondo', 'Parrilladas', 'Fuentes de parrillada', 'Fuentes de chicharrón', 'Guarniciones', 'Bebidas', 'Vinos y cervezas', 'Licores');

COMMIT;

-- Verificación
SELECT c.Nombre AS Categoria, COUNT(p.ProductoId) AS Productos
FROM Categorias c LEFT JOIN Productos p ON p.CategoriaId = c.CategoriaId
GROUP BY c.CategoriaId, c.Nombre ORDER BY c.CategoriaId;
