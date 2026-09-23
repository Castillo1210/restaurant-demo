// ============================================================
//  cocinaService.js — Pantalla de cocina (KDS) en tiempo real
// ============================================================

import api, { getApiErrorMessage } from './api';

/** Pedidos activos en cocina (Pendiente, EnPreparacion, Listo). */
export const getPedidosCocina = async () => {
  const { data } = await api.get('/cocina/pedidos');
  return data;
};

/**
 * Cambia el estado de un ítem: 'Pendiente' | 'EnPreparacion' | 'Listo'.
 */
export const cambiarEstadoItem = async (pedidoId, detalleId, estado) => {
  try {
    const { data } = await api.patch(`/cocina/pedidos/${pedidoId}/items/${detalleId}/estado`, { estado });
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar el ítem'));
  }
};

/**
 * Mueve el pedido completo: 'EnPreparacion' ("Empezar") o 'Listo' ("Pedido listo").
 */
export const cambiarEstadoPedidoCocina = async (pedidoId, estado) => {
  try {
    const { data } = await api.patch(`/cocina/pedidos/${pedidoId}/estado`, { estado });
    return data;
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar el pedido'));
  }
};
