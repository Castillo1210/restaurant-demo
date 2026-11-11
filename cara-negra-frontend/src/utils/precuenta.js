// ============================================================
//  precuenta.js — NOTA DE PEDIDO (cuenta previa) en la ticketera de caja
//  Se entrega al cliente para que revise su consumo ANTES de pagar.
//  No cobra, no cierra el pedido ni emite comprobante: el pedido
//  sigue abierto y se pueden agregar platos después.
// ============================================================

import { imprimirTicket, escaparHtml as e } from './imprimir';
import { etiquetaMesa } from './mesaLabel';

const S = (n) => `S/ ${Number(n ?? 0).toFixed(2)}`;

const fechaHora = () =>
  new Date().toLocaleString('es-PE', {
    timeZone: 'America/Lima', day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit',
  });

/**
 * Agrupa las líneas del pedido por producto y precio ("1x Cuy" + "1x Cuy" → "2x Cuy"),
 * sin los ítems cancelados.
 */
export function lineasPrecuenta(detalles) {
  const grupos = new Map();
  for (const d of detalles ?? []) {
    if (d.estado === 'Cancelado') continue;
    const clave = `${d.productoId}__${d.monto}`;
    const g = grupos.get(clave);
    if (g) g.cantidad += d.cantidad;
    else grupos.set(clave, { nombre: d.productoNombre, cantidad: d.cantidad, precio: d.monto });
  }
  return [...grupos.values()];
}

/**
 * Imprime la nota de pedido (cuenta previa) con el formato del restaurante: logo, razón social,
 * dirección y RUC; quién atiende; cliente genérico (sin DNI: es una cuenta previa, no un
 * comprobante); tabla CANT. / DESCRIPCIÓN / P.U. / TOTAL; TOTAL A PAGAR (lo único en negrita del
 * cuerpo) y, al pie, el número de orden con la mesa y la hora.
 * @param {object} pedido Pedido de Caja (detalles, descuento, total…).
 * @param {{ nombreComercial?: string, razonSocial?: string, direccion?: string, ruc?: string, logo?: string|null }} [empresa]
 */
export function imprimirPrecuenta(pedido, { nombreComercial, razonSocial, direccion, ruc, logo } = {}) {
  const lineas = lineasPrecuenta(pedido.detalles);
  const n2 = (v) => Number(v ?? 0).toFixed(2);

  const el = document.createElement('div');
  el.className = 'ticket ticket-precuenta';
  el.innerHTML = `
    <div class="pc-centro">
      ${logo
        ? `<img class="ticket-logo pc-logo" src="${e(logo)}" alt="">`
        : `<div class="pc-grande">${e(nombreComercial || 'Restaurante')}</div>`}
      ${razonSocial ? `<div class="pc-mayus">${e(razonSocial)}</div>` : ''}
      ${direccion ? `<div class="pc-mayus">${e(direccion)}</div>` : ''}
      ${ruc ? `<div class="pc-fuerte pc-ruc">RUC:${e(ruc)}</div>` : ''}
    </div>
    <div class="pc-sep pc-sep-corta"></div>
    ${pedido.usuarioNombre ? `<div class="pc-centro">Atendido por ${e(pedido.usuarioNombre)}</div>` : ''}
    <div class="pc-cliente">
      <div>CLIENTE</div>
      <div>CLIENTES VARIOS</div>
    </div>
    <div class="pc-sep"></div>
    <div class="pc-cabecera"><span>CANT.</span><span>DESCRIPCIÓN</span><span>P.U.</span><span>TOTAL</span></div>
    <div class="pc-sep"></div>
    ${lineas.map((l) => `
      <div class="pc-linea"><span>${e(l.cantidad)}</span><span>${e(l.nombre)}</span><span>${n2(l.precio)}</span><span>${n2(l.cantidad * l.precio)}</span></div>
    `).join('')}
    <div class="pc-sep"></div>
    ${pedido.descuento ? `
      <div class="pc-fila"><span>SUBTOTAL</span><span>${S(pedido.subTotal)}</span></div>
      <div class="pc-fila"><span>DESCUENTO ${e(pedido.descuento.nombre)}</span><span>- ${S(pedido.descuento.montoDescuento)}</span></div>` : ''}
    <div class="pc-fila pc-total"><span>TOTAL A PAGAR</span><span>${S(pedido.total)}</span></div>
    <div class="pc-orden">Orden ${e(pedido.id)} · ${e(etiquetaMesa(pedido.salonNombre, pedido.mesaNumero))}</div>
    <div class="pc-centro pc-chico">${e(fechaHora())}</div>
  `;
  return imprimirTicket(el);
}
