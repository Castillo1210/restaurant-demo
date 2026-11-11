// ============================================================
//  tema.js — Tema de colores por equipo: 'claro' (por defecto) u
//  'oscuro'. Se guarda en este navegador (cada celular, tablet o
//  la PC de caja elige el suyo; p. ej. la TV de cocina en oscuro).
//  index.html lo aplica antes de pintar, para que no parpadee.
// ============================================================

const CLAVE = 'tema';
const COLOR_BARRA = { claro: '#f3f4f6', oscuro: '#0c0c0c' };

export const getTema = () => {
  try {
    return localStorage.getItem(CLAVE) === 'oscuro' ? 'oscuro' : 'claro';
  } catch {
    return 'claro';
  }
};

export const aplicarTema = (tema) => {
  const t = tema === 'oscuro' ? 'oscuro' : 'claro';
  document.documentElement.dataset.tema = t;
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', COLOR_BARRA[t]);
  return t;
};

export const setTema = (tema) => {
  const t = aplicarTema(tema);
  try {
    localStorage.setItem(CLAVE, t);
  } catch {
    /* sin almacenamiento: solo para esta sesión */
  }
  return t;
};
