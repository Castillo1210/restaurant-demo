// ============================================================
//  categoryIcons.js — Mapeo de emoji por palabra clave para las
//  categorías del menú. El backend (CategoriaDto) no tiene un
//  campo "icon" (solo Nombre/Descripcion), así que este es un
//  fallback puramente visual en el cliente, basado en el nombre
//  de la categoría que cargue el administrador desde el panel.
// ============================================================

const ICON_RULES = [
  { keywords: ['hamburgues', 'burger'], icon: '🍔' },
  { keywords: ['entrada', 'aperitivo', 'picoteo'], icon: '🥗' },
  { keywords: ['principal', 'plato fuerte', 'fuerte'], icon: '🍖' },
  { keywords: ['postre', 'dulce'], icon: '🍮' },
  { keywords: ['bebida', 'gaseosa', 'refresco', 'jugo'], icon: '🥤' },
  { keywords: ['cerveza', 'alcohol', 'licor'], icon: '🍺' },
  { keywords: ['papa', 'frita', 'acompañam', 'guarnic'], icon: '🍟' },
  { keywords: ['pollo'], icon: '🍗' },
  { keywords: ['ensalada'], icon: '🥗' },
  { keywords: ['pizza'], icon: '🍕' },
  { keywords: ['caf', 'infusion'], icon: '☕' },
];

const DEFAULT_ICON = '🍽️';

export const getCategoryIcon = (nombreCategoria = '') => {
  const nombre = nombreCategoria.toLowerCase();
  const match = ICON_RULES.find((rule) => rule.keywords.some((kw) => nombre.includes(kw)));
  return match?.icon ?? DEFAULT_ICON;
};
