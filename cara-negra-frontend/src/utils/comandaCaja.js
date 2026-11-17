// ============================================================
//  comandaCaja.js — Comanda impresa en la ticketera de CAJA
//  (la de boletas, impresora predeterminada de Windows).
//  Sale sola para los pedidos del administrador (en vez de cocina) y,
//  a pedido, como copia para el cajero.
//
//  Sale IGUAL que la de la ticketera de cocina: misma letra (Carlito),
//  mismos tamaños en milímetros (GET /impresion/comanda, que sigue
//  ImpresoraCocina:TamanoProductos), cada plato en una sola línea (si
//  no entra, se achica esa línea) y solo el nombre del mozo en negrita.
// ============================================================

import api from '../services/api';
import { imprimirTicket, escaparHtml as escapar } from './imprimir';
import { etiquetaMesa, salonTieneNombreCompuesto } from './mesaLabel';

const FUENTE = 'Carlito';
const PX_POR_MM = 96 / 25.4;

// Si no se puede consultar el formato (sin conexión), las medidas de "Intermedia" en 80 mm.
const FORMATO_POR_DEFECTO = {
  anchoMm: 72,
  margenMm: 0.5,
  encabezadoMm: 4,
  normalMm: 3,
  productosMm: 3.75,
  anuladosMm: 2.75,
  interlineado: 1.2,
};

let formatoPromesa = null;
const obtenerFormato = () => {
  if (!formatoPromesa) {
    formatoPromesa = api
      .get('/impresion/comanda')
      .then(({ data }) => ({ ...FORMATO_POR_DEFECTO, ...data }))
      .catch(() => {
        formatoPromesa = null; // se vuelve a intentar en la próxima comanda
        return FORMATO_POR_DEFECTO;
      });
  }
  return formatoPromesa;
};

/** La letra tiene que estar cargada antes de medir e imprimir. */
const cargarFuente = () =>
  Promise.all([
    document.fonts?.load(`400 12px ${FUENTE}`),
    document.fonts?.load(`700 12px ${FUENTE}`),
  ]).catch(() => {});

let lienzo = null;
/** Ancho del texto en mm con la letra de la comanda. */
const medirMm = (texto, mm, negrita) => {
  lienzo ??= document.createElement('canvas');
  const ctx = lienzo.getContext('2d');
  ctx.font = `${negrita ? 700 : 400} ${mm * PX_POR_MM}px ${FUENTE}, Arial, sans-serif`;
  return ctx.measureText(texto).width / PX_POR_MM;
};

/** Hora como en la ticketera de cocina: "15/11 15:25" (hora de Lima, 24 h). */
const horaComanda = (fecha) => {
  const partes = Object.fromEntries(
    new Intl.DateTimeFormat('es-PE', {
      timeZone: 'America/Lima', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
    })
      .formatToParts(new Date(fecha ?? Date.now()))
      .map((p) => [p.type, p.value]),
  );
  return `${partes.day}/${partes.month} ${partes.hour}:${partes.minute}`;
};

/**
 * Una línea de la comanda.
 * @param {object} f formato
 * @param {{ tramos: Array<{texto:string, negrita?:boolean}>, mm:number, centrada?:boolean,
 *           derecha?:string, envolver?:boolean }} o
 *   envolver: el texto puede ocupar varias líneas (notas largas); si no, va en UNA línea y se achica.
 */
const linea = (f, { tramos, mm, centrada = false, derecha = null, envolver = false }) => {
  const util = f.anchoMm - 2 * f.margenMm;
  let tam = mm;
  if (!envolver) {
    const ancho =
      tramos.reduce((a, t) => a + medirMm(t.texto, mm, t.negrita), 0) + (derecha ? medirMm(`   ${derecha}`, mm, false) : 0);
    if (ancho > util) tam = (mm * util) / ancho;
  }
  const html = tramos.map((t) => (t.negrita ? `<b>${escapar(t.texto)}</b>` : escapar(t.texto))).join('');
  const estilo = `font-size:${tam.toFixed(3)}mm;line-height:${f.interlineado}`;
  const clases = ['tc-linea', centrada && 'tc-centro', envolver && 'tc-envolver', derecha && 'tc-dos'].filter(Boolean).join(' ');
  return derecha
    ? `<div class="${clases}" style="${estilo}"><span>${html}</span><span>${escapar(derecha)}</span></div>`
    : `<div class="${clases}" style="${estilo}">${html}</div>`;
};

const SEPARADOR = '<div class="tc-sep"></div>';

/** Encabezado común: aviso (ADICIONAL, ANULADO…), mesa, salón compuesto y la línea del mozo. */
const encabezado = (f, { aviso, salonNombre, mesaNumero, rotulo, persona, fecha }) => {
  const l = [];
  if (aviso) l.push(linea(f, { tramos: [{ texto: aviso, negrita: true }], mm: f.encabezadoMm, centrada: true }));
  l.push(linea(f, {
    tramos: [{ texto: etiquetaMesa(salonNombre, mesaNumero).toUpperCase(), negrita: true }],
    mm: f.encabezadoMm,
    centrada: true,
  }));
  if (salonTieneNombreCompuesto(salonNombre)) {
    l.push(linea(f, { tramos: [{ texto: `(${salonNombre})` }], mm: f.normalMm, centrada: true }));
  }
  l.push(linea(f, {
    tramos: persona ? [{ texto: `${rotulo}: ` }, { texto: persona, negrita: true }] : [{ texto: '' }],
    mm: f.encabezadoMm,
    derecha: horaComanda(fecha),
  }));
  return l;
};

const crearTicket = (f, lineas) => {
  const el = document.createElement('div');
  el.className = 'ticket ticket-comanda';
  el.style.width = `${f.anchoMm}mm`;
  el.innerHTML = lineas.join('');
  return el;
};

/**
 * Imprime la comanda en la ticketera de caja.
 * @param {{ pedidoId:number|string, salonNombre?:string, mesaNumero?:string, atendidoPor?:string,
 *           nombreComercial?:string, esAdicional?:boolean, fecha?:string|Date, comentario?:string,
 *           items: Array<{ cantidad:number, nombre:string, notas?:string }> }} datos
 */
export async function imprimirComandaEnCaja(datos) {
  const items = (datos.items ?? []).filter((i) => i && i.cantidad > 0);
  const totalUnidades = items.reduce((acc, i) => acc + Number(i.cantidad), 0);
  const [f] = await Promise.all([obtenerFormato(), cargarFuente()]);

  const lineas = encabezado(f, {
    aviso: datos.esAdicional ? '** ADICIONAL **' : null,
    salonNombre: datos.salonNombre,
    mesaNumero: datos.mesaNumero,
    rotulo: 'Mozo',
    persona: datos.atendidoPor,
    fecha: datos.fecha,
  });
  lineas.push(SEPARADOR);

  const comentario = String(datos.comentario ?? '').trim();
  if (comentario) {
    lineas.push(linea(f, { tramos: [{ texto: `NOTA: ${comentario}`, negrita: true }], mm: f.encabezadoMm, envolver: true }));
    lineas.push(SEPARADOR);
  }

  for (const i of items) {
    lineas.push(linea(f, { tramos: [{ texto: `${i.cantidad} x ${i.nombre}` }], mm: f.productosMm }));
    const notas = String(i.notas ?? '').trim();
    if (notas) lineas.push(linea(f, { tramos: [{ texto: `>> ${notas}` }], mm: f.productosMm }));
  }

  lineas.push(SEPARADOR);
  lineas.push(linea(f, {
    tramos: [{
      texto: `${totalUnidades} unidad${totalUnidades === 1 ? '' : 'es'} · ${items.length} producto${items.length === 1 ? '' : 's'}`,
    }],
    mm: f.normalMm,
    centrada: true,
  }));

  return imprimirTicket(crearTicket(f, lineas));
}

/**
 * Aviso de anulación en la ticketera de caja (cuando anula o quita platos el administrador o el
 * cajero: su equipo lo imprime y no la ticketera de cocina). Mismo formato que el de cocina.
 * @param {{ salonNombre?:string, mesaNumero?:string, anulo?:string, motivo?:string,
 *           pedidoCompleto?:boolean, fecha?:string|Date, items: Array<{ cantidad:number, nombre:string }> }} datos
 */
export async function imprimirAnulacionEnCaja(datos) {
  const items = (datos.items ?? []).filter((i) => i && i.cantidad > 0);
  if (items.length === 0) return;
  const [f] = await Promise.all([obtenerFormato(), cargarFuente()]);

  const lineas = encabezado(f, {
    aviso: datos.pedidoCompleto ? '** PEDIDO ANULADO **' : '** ANULADO **',
    salonNombre: datos.salonNombre,
    mesaNumero: datos.mesaNumero,
    rotulo: 'Anuló',
    persona: datos.anulo,
    fecha: datos.fecha,
  });
  if (datos.motivo) {
    lineas.push(linea(f, { tramos: [{ texto: `Motivo: ${datos.motivo}`, negrita: true }], mm: f.normalMm, envolver: true }));
  }
  lineas.push(SEPARADOR);
  for (const i of items) {
    lineas.push(linea(f, { tramos: [{ texto: `${i.cantidad} x ${i.nombre}` }], mm: f.anuladosMm }));
  }
  lineas.push(SEPARADOR);
  lineas.push(linea(f, { tramos: [{ texto: 'NO PREPARAR', negrita: true }], mm: f.encabezadoMm, centrada: true }));

  return imprimirTicket(crearTicket(f, lineas));
}
