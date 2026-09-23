// ============================================================
//  reportesService.js — Reportes de ventas (backend real, solo ADMIN)
//  Consume GET /reportes/resumen-ventas y GET /reportes/productos-mas-vendidos.
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapResumen = (r) => ({
  fechaDesde: r.fechaDesde,
  fechaHasta: r.fechaHasta,
  totalVentas: r.totalVentas,
  cantidadPedidos: r.cantidadPedidos,
  cantidadPedidosCancelados: r.cantidadPedidosCancelados,
  cantidadPedidosPagados: r.cantidadPedidosPagados,
  ticketPromedio: r.ticketPromedio,
  totalDescuentos: r.totalDescuentos,
  ventasPorMetodoPago: (r.ventasPorMetodoPago ?? []).map((v) => ({
    metodoPagoNombre: v.metodoPagoNombre,
    total: v.total,
    cantidadPagos: v.cantidadPagos,
  })),
});

const mapProductoMasVendido = (p) => ({
  productoId: p.productoId,
  productoNombre: p.productoNombre,
  categoriaNombre: p.categoriaNombre,
  cantidadVendida: p.cantidadVendida,
  totalVendido: p.totalVendido,
});

/**
 * Formatea una fecha JS a 'YYYY-MM-DD' (formato que espera el backend en query string).
 * @param {Date} date
 */
const toDateParam = (date) => {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
};

/**
 * @param {Date} fechaDesde
 * @param {Date} fechaHasta
 */
export const getResumenVentas = async (fechaDesde, fechaHasta) => {
  try {
    const { data } = await api.get('/reportes/resumen-ventas', {
      params: { fechaDesde: toDateParam(fechaDesde), fechaHasta: toDateParam(fechaHasta) },
    });
    return mapResumen(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo cargar el resumen de ventas'));
  }
};

/**
 * @param {Date} fechaDesde
 * @param {Date} fechaHasta
 * @param {number} top
 */
export const getProductosMasVendidos = async (fechaDesde, fechaHasta, top = 10) => {
  try {
    const { data } = await api.get('/reportes/productos-mas-vendidos', {
      params: { fechaDesde: toDateParam(fechaDesde), fechaHasta: toDateParam(fechaHasta), top },
    });
    return data.map(mapProductoMasVendido);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los productos más vendidos'));
  }
};

const descargarArchivo = (blob, nombreArchivo) => {
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nombreArchivo;
  document.body.appendChild(a);
  a.click();
  a.remove();
  window.URL.revokeObjectURL(url);
};

/**
 * Descarga un .xlsx con el resumen de ventas + productos más vendidos del rango indicado.
 * @param {Date} fechaDesde
 * @param {Date} fechaHasta
 */
export const exportarResumenVentas = async (fechaDesde, fechaHasta) => {
  try {
    const { data } = await api.get('/reportes/exportar', {
      params: { fechaDesde: toDateParam(fechaDesde), fechaHasta: toDateParam(fechaHasta) },
      responseType: 'blob',
    });
    descargarArchivo(data, `reporte-ventas_${toDateParam(fechaDesde)}_a_${toDateParam(fechaHasta)}.xlsx`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo exportar el reporte'));
  }
};

/**
 * Descarga un .xlsx con el detalle línea por línea de los pedidos de una mesa.
 * @param {number} mesaId
 * @param {string|number} mesaNumero solo para nombrar el archivo
 * @param {Date} [fechaDesde]
 * @param {Date} [fechaHasta]
 */
export const exportarPedidosPorMesa = async (mesaId, mesaNumero, fechaDesde, fechaHasta) => {
  try {
    const { data } = await api.get('/reportes/pedidos-por-mesa/exportar', {
      params: {
        mesaId,
        fechaDesde: fechaDesde ? toDateParam(fechaDesde) : undefined,
        fechaHasta: fechaHasta ? toDateParam(fechaHasta) : undefined,
      },
      responseType: 'blob',
    });
    descargarArchivo(data, `pedidos-mesa-${mesaNumero}.xlsx`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo exportar los pedidos de la mesa'));
  }
};
