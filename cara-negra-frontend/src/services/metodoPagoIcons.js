// ============================================================
//  metodoPagoIcons.js — Emoji por nombre de método de pago, para
//  el selector visual de caja. El backend (MetodoPagoDto) no tiene
//  un campo "icon" (solo Nombre/EstaActivo), así que este es un
//  fallback puramente visual en el cliente, igual que categoryIcons.js.
// ============================================================

const ICON_RULES = [
  { keywords: ['efectivo', 'cash'], icon: '💵' },
  { keywords: ['tarjeta', 'visa', 'mastercard', 'card'], icon: '💳' },
  { keywords: ['yape'], icon: '📱' },
  { keywords: ['plin'], icon: '📲' },
  { keywords: ['transferencia', 'deposito', 'banco'], icon: '🏦' },
];

const DEFAULT_ICON = '💰';

export const getMetodoPagoIcon = (nombreMetodo = '') => {
  const nombre = nombreMetodo.toLowerCase();
  const match = ICON_RULES.find((rule) => rule.keywords.some((kw) => nombre.includes(kw)));
  return match?.icon ?? DEFAULT_ICON;
};
