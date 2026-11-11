// ============================================================
//  ladoPanel.js — En pantallas grandes (monitor de caja), de qué lado
//  va el listado del pedido junto a la carta: 'derecha' (por defecto)
//  o 'izquierda'. Se guarda en este equipo, como el tema: sirve cuando
//  algo tapa un costado del monitor.
// ============================================================

const CLAVE = 'ladoPanelPedido';
const EVENTO = 'lado-panel-cambiado';

export const getLadoPanel = () => {
  try {
    return localStorage.getItem(CLAVE) === 'izquierda' ? 'izquierda' : 'derecha';
  } catch {
    return 'derecha';
  }
};

export const setLadoPanel = (lado) => {
  const l = lado === 'izquierda' ? 'izquierda' : 'derecha';
  try {
    localStorage.setItem(CLAVE, l);
  } catch {
    /* sin almacenamiento: solo para esta sesión */
  }
  window.dispatchEvent(new CustomEvent(EVENTO, { detail: l }));
  return l;
};

/** Se llama con el lado nuevo cada vez que cambia (en esta pestaña). Devuelve cómo dejar de escuchar. */
export const alCambiarLadoPanel = (fn) => {
  const manejar = (e) => fn(e.detail);
  window.addEventListener(EVENTO, manejar);
  return () => window.removeEventListener(EVENTO, manejar);
};
