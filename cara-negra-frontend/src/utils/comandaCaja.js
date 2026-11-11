// ============================================================
//  comandaCaja.js — Comanda impresa en la ticketera de CAJA
//  (la de boletas, impresora predeterminada de Windows).
//  Sale sola para los pedidos del administrador (en vez de cocina) y,
//  a pedido, como copia para el cajero. Formato compacto, igual que
//  el de cocina: sin nombre del restaurante ni número de pedido.
// ============================================================

import { imprimirTicket, escaparHtml as escapar } from './imprimir';
import { etiquetaMesa, salonTieneNombreCompuesto } from './mesaLabel';

const horaLima = (fecha) =>
  new Date(fecha ?? Date.now()).toLocaleString('es-PE', {
    timeZone: 'America/Lima', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  });

/**
 * Imprime la comanda en la ticketera de caja.
 * @param {{ pedidoId:number|string, salonNombre?:string, mesaNumero?:string, atendidoPor?:string,
 *           nombreComercial?:string, esAdicional?:boolean, fecha?:string|Date, comentario?:string,
 *           items: Array<{ cantidad:number, nombre:string, notas?:string }> }} datos
 */
export function imprimirComandaEnCaja(datos) {
  const items = (datos.items ?? []).filter((i) => i && i.cantidad > 0);
  const totalUnidades = items.reduce((acc, i) => acc + Number(i.cantidad), 0);

  const el = document.createElement('div');
  el.className = 'ticket ticket-comanda';
  el.innerHTML = `
    ${datos.esAdicional ? '<div class="tc-adicional">** ADICIONAL **</div>' : ''}
    <div class="tc-mesa">${escapar(etiquetaMesa(datos.salonNombre, datos.mesaNumero).toUpperCase())}</div>
    ${salonTieneNombreCompuesto(datos.salonNombre) ? `<div class="tc-centro">(${escapar(datos.salonNombre)})</div>` : ''}
    <div class="tc-fila"><span>${datos.atendidoPor ? `Mozo: ${escapar(datos.atendidoPor)}` : ''}</span><span>${escapar(horaLima(datos.fecha))}</span></div>
    <div class="tc-sep"></div>
    ${datos.comentario && String(datos.comentario).trim()
      ? `<div class="tc-comentario">NOTA: ${escapar(String(datos.comentario).trim())}</div><div class="tc-sep"></div>`
      : ''}
    ${items.map((i) => `
      <div class="tc-item"><span class="tc-cant">${escapar(i.cantidad)} x</span><span>${escapar(i.nombre)}</span></div>
      ${i.notas && String(i.notas).trim() ? `<div class="tc-nota">&gt;&gt; ${escapar(String(i.notas).trim())}</div>` : ''}
    `).join('')}
    <div class="tc-sep"></div>
    <div class="tc-centro">${totalUnidades} unidad${totalUnidades === 1 ? '' : 'es'} - ${items.length} producto${items.length === 1 ? '' : 's'}</div>
  `;
  return imprimirTicket(el);
}

/**
 * Aviso de anulación en la ticketera de caja (cuando anula o quita platos el administrador o el
 * cajero: su equipo lo imprime y no la ticketera de cocina). Mismo formato que el de cocina.
 * @param {{ salonNombre?:string, mesaNumero?:string, anulo?:string, motivo?:string,
 *           pedidoCompleto?:boolean, fecha?:string|Date, items: Array<{ cantidad:number, nombre:string }> }} datos
 */
export function imprimirAnulacionEnCaja(datos) {
  const items = (datos.items ?? []).filter((i) => i && i.cantidad > 0);
  if (items.length === 0) return Promise.resolve();

  const el = document.createElement('div');
  el.className = 'ticket ticket-comanda';
  el.innerHTML = `
    <div class="tc-adicional">${datos.pedidoCompleto ? '** PEDIDO ANULADO **' : '** ANULADO **'}</div>
    <div class="tc-mesa">${escapar(etiquetaMesa(datos.salonNombre, datos.mesaNumero).toUpperCase())}</div>
    <div class="tc-fila"><span>${datos.anulo ? `Anuló: ${escapar(datos.anulo)}` : ''}</span><span>${escapar(horaLima(datos.fecha))}</span></div>
    ${datos.motivo ? `<div class="tc-comentario">Motivo: ${escapar(datos.motivo)}</div>` : ''}
    <div class="tc-sep"></div>
    ${items.map((i) => `
      <div class="tc-item tc-item-normal"><span class="tc-cant">${escapar(i.cantidad)} x</span><span>${escapar(i.nombre)}</span></div>
    `).join('')}
    <div class="tc-sep"></div>
    <div class="tc-adicional">NO PREPARAR</div>
  `;
  return imprimirTicket(el);
}
