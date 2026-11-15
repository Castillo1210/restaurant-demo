/**
 * Pantalla de inicio de cada rol (después de entrar o si abre una pantalla que no le corresponde):
 *   COCINA   → el tablero de cocina
 *   TAQUILLA → la venta rápida de la piscina
 *   el resto → el mapa de mesas
 */
export const rutaInicio = (rol) => {
  if (rol === 'COCINA') return '/cocina';
  if (rol === 'TAQUILLA') return '/venta-rapida';
  return '/dashboard';
};
