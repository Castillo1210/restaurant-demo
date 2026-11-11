// ============================================================
//  ordersService.js — Servicio de pedidos (backend real)
//  Reemplaza el mock basado en localStorage por llamadas reales
//  a /pedidos (toma de orden del mozo, cambios de estado, items).
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapDetalle = (d) => ({
  id: d.detallePedidoId,
  productoId: d.productoId,
  productoNombre: d.productoNombre,
  cantidad: d.cantidad,
  monto: d.monto,
  notas: d.notas ?? '',
  estado: d.estadoDetallePedido,
});

const mapPago = (p) => ({
  id: p.pagoId,
  pedidoId: p.pedidoId,
  monto: p.monto,
  metodoPagoId: p.metodoPagoId,
  metodoPagoNombre: p.metodoPagoNombre,
  referencia: p.referencia,
  anulado: p.estaAnulado,
  motivoAnulacion: p.motivoAnulacion,
  creadoEn: p.creadoEn,
});

const mapDescuento = (d) =>
  d
    ? {
        descuentoId: d.descuentoId,
        nombre: d.nombre,
        esPorcentaje: d.esPorcentaje,
        valor: d.valor,
        montoDescuento: d.montoDescuento,
      }
    : null;

const mapPedido = (p) => ({
  id: p.pedidoId,
  mesaId: p.mesaId,
  mesaNumero: p.mesaNumero,
  salonNombre: p.salonNombre ?? '',
  usuarioId: p.usuarioId,
  usuarioNombre: p.usuarioNombre,
  subTotal: p.subTotal,
  total: p.total,
  estado: p.estadoPedido,
  detalles: (p.detalles ?? []).map(mapDetalle),
  pagos: (p.pagos ?? []).map(mapPago),
  descuento: mapDescuento(p.descuento),
  comentario: p.comentario ?? '',
  creadoEn: p.creadoEn,
});

/**
 * Crea un nuevo pedido (toma de orden). Ocupa la mesa automáticamente.
 * @param {{ mesaId: number, usuarioId: number, detalles: Array<{productoId:number, cantidad:number, notas?:string}>, comentario?: string }} payload
 *   comentario = comentario general del pedido ("todo para llevar"), opcional.
 * @returns {Promise<Object>} pedido creado
 */
export const createPedido = async ({ mesaId, usuarioId, detalles, comentario, operacionId }) => {
  try {
    const { data } = await api.post('/pedidos', {
      mesaId,
      usuarioId,
      comentario: comentario?.trim() || null,
      // Código del envío: si se reintenta por el Wi-Fi, el servidor no crea otro pedido.
      ...(operacionId ? { operacionId } : {}),
      detalles: detalles.map((d) => ({
        productoId: d.productoId,
        cantidad: d.cantidad,
        notas: d.notas || null,
        // Precio distinto al de la carta (solo cajero / admin; el servidor lo valida).
        ...(d.precioUnitario != null ? { precioUnitario: d.precioUnitario } : {}),
      })),
    });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo enviar el pedido'));
  }
};

/**
 * Obtiene un pedido por su ID, con detalles y pagos.
 * @param {number} id
 * @returns {Promise<Object>}
 */
export const getPedidoById = async (id) => {
  const [{ data }, anulados, cambiosPrecio] = await Promise.all([
    api.get(`/pedidos/${id}`), getAnulacionesPedido(id), getCambiosPrecioPedido(id),
  ]);
  return { ...mapPedido(data), anulados, cambiosPrecio };
};

/** Precios distintos a la carta en un pedido (quién, cuándo, de cuánto a cuánto). [] si falla. */
export const getCambiosPrecioPedido = async (pedidoId) => {
  try {
    const { data } = await api.get(`/pedidos/${pedidoId}/cambios-precio`);
    return Array.isArray(data) ? data : [];
  } catch {
    return [];
  }
};

/**
 * Cambia el precio de una línea ya enviada (solo cajero / admin, sin pagos registrados).
 * Queda registrado con el nombre de quien lo hizo.
 * @param {number} pedidoId
 * @param {number} detalleId
 * @param {{ precio: number, motivo?: string }} datos
 */
export const cambiarPrecioDetalle = async (pedidoId, detalleId, { precio, motivo }) => {
  try {
    const { data } = await api.put(`/pedidos/${pedidoId}/detalles/${detalleId}/precio`, { precio, motivo: motivo || null });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo cambiar el precio'));
  }
};

/**
 * Productos quitados de un pedido (quién, cuándo, cuánto y por qué), del más reciente al más
 * antiguo. Si falla devuelve [] (es información de control, no debe impedir trabajar).
 * @param {number} pedidoId
 */
export const getAnulacionesPedido = async (pedidoId) => {
  try {
    const { data } = await api.get(`/pedidos/${pedidoId}/anulados`);
    return Array.isArray(data) ? data : [];
  } catch {
    return [];
  }
};

/**
 * Lista pedidos con filtros opcionales (estado, mesa, fechas).
 * @param {{ page?: number, pageSize?: number, estado?: string, mesaId?: number }} params
 */
export const getPedidos = async (params = {}) => {
  const { data } = await api.get('/pedidos', { params });
  return {
    items: (data.items ?? []).map(mapPedido),
    totalCount: data.totalCount,
    page: data.page,
    pageSize: data.pageSize,
    totalPages: data.totalPages,
  };
};

/**
 * Busca el pedido activo (no Entregado ni Cancelado) de una mesa que ya está
 * Ocupada, para poder agregarle más ítems en vez de crear un pedido duplicado.
 * @param {number} mesaId
 * @returns {Promise<Object|null>}
 */
export const getActivePedidoForMesa = async (mesaId) => {
  const { data } = await api.get('/pedidos', { params: { mesaId, pageSize: 10 } });
  const activo = (data.items ?? []).find(
    (p) => p.estadoPedido !== 'Entregado' && p.estadoPedido !== 'Cancelado'
  );
  return activo ? mapPedido(activo) : null;
};

/**
 * Cambia el estado de un pedido (Pendiente → EnPreparacion → Listo, o Cancelado).
 * @param {number} pedidoId
 * @param {'Pendiente'|'EnPreparacion'|'Listo'|'Cancelado'} estadoPedido
 */
export const cambiarEstadoPedido = async (pedidoId, estadoPedido) => {
  try {
    const { data } = await api.patch(`/pedidos/${pedidoId}/estado`, { estadoPedido });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo cambiar el estado del pedido'));
  }
};

/**
 * Agrega un ítem a un pedido existente (Pendiente o En Preparación).
 * @param {number} pedidoId
 * @param {{ productoId: number, cantidad: number, notas?: string }} item
 */
export const agregarDetalle = async (pedidoId, { productoId, cantidad, notas }) => {
  try {
    const { data } = await api.post(`/pedidos/${pedidoId}/detalles`, {
      productoId,
      cantidad,
      notas: notas || null,
    });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo agregar el ítem al pedido'));
  }
};

/**
 * Agrega VARIOS ítems a un pedido en una sola petición (todo o nada, una sola comanda).
 * @param {number} pedidoId
 * @param {{productoId:number, cantidad:number, notas?:string}[]} detalles
 * @param {string} [comentario] comentario general del pedido: undefined = no se toca, '' lo borra
 */
export const agregarDetalles = async (pedidoId, detalles, comentario, operacionId) => {
  try {
    const { data } = await api.post(`/pedidos/${pedidoId}/detalles/lote`, {
      ...(comentario !== undefined ? { comentario: comentario.trim() } : {}),
      // Código del envío: si se reintenta por el Wi-Fi, el servidor no duplica los platos.
      ...(operacionId ? { operacionId } : {}),
      detalles: detalles.map((d) => ({
        productoId: d.productoId,
        cantidad: d.cantidad,
        notas: d.notas || null,
        ...(d.precioUnitario != null ? { precioUnitario: d.precioUnitario } : {}),
      })),
    });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron agregar los ítems al pedido'));
  }
};

/** Máximo del comentario general del pedido (igual que en el servidor). */
export const COMENTARIO_MAXIMO = 300;

/**
 * Cambia el comentario general del pedido ("todo para llevar"). '' lo borra. Si queda un
 * comentario, cocina recibe un ticket corto con él. El mozo solo en sus propias mesas.
 * @param {number} pedidoId
 * @param {string} comentario
 */
export const actualizarComentarioPedido = async (pedidoId, comentario) => {
  try {
    const { data } = await api.put(`/pedidos/${pedidoId}/comentario`, { comentario: comentario?.trim() ?? '' });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo guardar el comentario del pedido'));
  }
};

/**
 * Quita (anula) un ítem de un pedido Pendiente o En Preparación. Queda registrado quién lo hizo,
 * cuándo y el motivo (obligatorio); cocina recibe un aviso "ANULADO".
 * @param {number} pedidoId
 * @param {number} detalleId
 * @param {{ motivo: string, cantidad?: number }} datos cantidad = unidades a quitar (todas si se omite)
 */
export const eliminarDetalle = async (pedidoId, detalleId, { motivo, cantidad } = {}) => {
  try {
    const { data } = await api.delete(`/pedidos/${pedidoId}/detalles/${detalleId}`, {
      params: { motivo, ...(cantidad ? { cantidad } : {}) },
    });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo quitar el ítem del pedido'));
  }
};

/**
 * Anula el pedido COMPLETO (motivo obligatorio): cada plato queda registrado como anulado, a
 * cocina le sale "PEDIDO ANULADO" y la mesa queda libre. No se puede si tiene un pago activo.
 * @param {number} pedidoId
 * @param {string} motivo
 */
export const anularPedido = async (pedidoId, motivo, avisarCocina = true) => {
  try {
    const { data } = await api.post(`/pedidos/${pedidoId}/anular`, { motivo, avisarCocina });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo anular el pedido'));
  }
};

/**
 * Aplica un descuento del catálogo a un pedido (Fase 7). Solo antes de registrar pagos.
 * @param {number} pedidoId
 * @param {number} descuentoId
 */
export const aplicarDescuento = async (pedidoId, descuentoId) => {
  try {
    const { data } = await api.post(`/pedidos/${pedidoId}/descuento`, { descuentoId });
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo aplicar el descuento'));
  }
};

/**
 * Quita el descuento aplicado a un pedido (Fase 7), si aún no tiene pagos registrados.
 * @param {number} pedidoId
 */
export const quitarDescuento = async (pedidoId) => {
  try {
    const { data } = await api.delete(`/pedidos/${pedidoId}/descuento`);
    return mapPedido(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo quitar el descuento'));
  }
};

/**
 * Reimprime manualmente la comanda de cocina de un pedido (Fase 6), por si la impresora
 * estaba apagada o sin papel al tomar el pedido. No lanza error si la impresora en sí
 * sigue sin responder: el backend registra el problema mas no falla el request.
 * @param {number} pedidoId
 */
export const reimprimirComanda = async (pedidoId) => {
  try {
    await api.post(`/pedidos/${pedidoId}/reimprimir`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo reimprimir la comanda'));
  }
};

/**
 * Trae el texto exacto de la comanda que se enviaría a la impresora de cocina, sin
 * imprimir nada — para poder ver/mostrar el formato del ticket (ej. en una demo al
 * cliente) sin depender de tener la impresora física conectada.
 * @param {number} pedidoId
 * @returns {Promise<string>}
 */
export const previsualizarComanda = async (pedidoId) => {
  try {
    const { data } = await api.get(`/pedidos/${pedidoId}/comanda-preview`);
    return data.texto;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo previsualizar la comanda'));
  }
};
