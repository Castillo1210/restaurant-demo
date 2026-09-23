// ============================================================
//  agruparDetalles.js — Consolida líneas de un pedido para mostrarlas
//  en el comprobante/panel de cobro.
//
//  Cada vez que se agrega un ítem a un pedido ya existente (mozo pide
//  algo, y minutos después pide el mismo producto otra vez), el backend
//  crea una fila DetallePedido nueva en vez de sumarla a la anterior
//  (a propósito: cada fila puede marcarse como preparada por separado
//  en cocina). Para el cliente, en cambio, ver "1x Hamburguesa" y más
//  abajo otra vez "1x Hamburguesa" en el mismo comprobante es confuso
//  — debería verse como una sola línea "2x Hamburguesa".
//
//  Se agrupa por producto + notas: dos pedidos del mismo producto con
//  notas distintas (ej. una "sin cebolla" y otra sin nota) se muestran
//  como líneas separadas a propósito, para no perder esa información.
// ============================================================

export function agruparDetallesPorProducto(detalles) {
  const grupos = new Map();
  const orden = [];

  for (const d of detalles ?? []) {
    const key = `${d.productoId}__${d.notas ?? ''}`;
    const existente = grupos.get(key);
    if (existente) {
      existente.cantidad += d.cantidad;
    } else {
      const copia = { ...d };
      grupos.set(key, copia);
      orden.push(key);
    }
  }

  return orden.map((key) => grupos.get(key));
}
