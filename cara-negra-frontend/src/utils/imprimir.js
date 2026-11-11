// ============================================================
//  imprimir.js — Impresión de tickets (boleta, factura, recibo,
//  comanda de caja, cierre) en la ticketera de 80 mm de la caja.
//
//  Se copia SOLO el ticket a un contenedor directo de <body> y, al
//  imprimir, todo lo demás se oculta con display:none. Antes se usaba
//  visibility:hidden, que oculta la app pero deja su ALTO: con un mapa
//  de 40 mesas la ticketera avanzaba metros de papel en blanco.
// ============================================================

const ID_ZONA = 'zona-impresion';

/**
 * Imprime un ticket.
 * @param {string|HTMLElement} elemento id del ticket o el elemento mismo
 */
export async function imprimirTicket(elemento) {
  const fuente = typeof elemento === 'string' ? document.getElementById(elemento) : elemento;
  if (!fuente) return;

  document.getElementById(ID_ZONA)?.remove();
  const zona = document.createElement('div');
  zona.id = ID_ZONA;
  const copia = fuente.cloneNode(true);
  copia.removeAttribute('id'); // sin ids repetidos en la página
  zona.appendChild(copia);
  document.body.appendChild(zona);
  document.body.classList.add('imprimiendo');

  // El logo y el QR (imágenes) deben estar listos antes de mandar a imprimir.
  await Promise.all(
    [...zona.querySelectorAll('img')].map((img) =>
      img.complete ? Promise.resolve() : (img.decode?.() ?? Promise.resolve()).catch(() => {})
    )
  );

  let limpio = false;
  const limpiar = () => {
    if (limpio) return;
    limpio = true;
    zona.remove();
    document.body.classList.remove('imprimiendo');
    window.removeEventListener('afterprint', limpiar);
  };
  window.addEventListener('afterprint', limpiar);
  window.print();
  // Respaldo por si el navegador no avisa el fin de la impresión.
  setTimeout(limpiar, 60000);
}

/** Escapa texto para armar tickets con HTML (nombres de platos, notas, clientes). */
export const escaparHtml = (texto) =>
  String(texto ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
