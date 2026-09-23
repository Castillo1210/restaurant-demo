// ============================================================
//  inventarioService.js — CRUD de artículos y movimientos de stock
//  (backend real, solo ADMIN). Módulo de inventario (Fase 6): Articulo
//  representa insumos/almacén, distinto de Producto (lo que se vende
//  en la carta). El stock nunca se edita directo: solo cambia
//  registrando un movimiento (Entrada/Salida/Ajuste).
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapArticulo = (a) => ({
  id: a.articuloId,
  nombre: a.nombre,
  descripcion: a.descripcion ?? '',
  precio: a.precio,
  stock: a.stock,
  activo: a.activo,
  tipo: a.tipo,
  categoriaId: a.categoriaId,
  categoriaNombre: a.categoriaNombre,
  creadoEn: a.creadoEn,
});

const mapMovimiento = (m) => ({
  id: m.movimientoArticuloId,
  articuloId: m.articuloId,
  articuloNombre: m.articuloNombre,
  tipo: m.tipoMovimiento, // 'Entrada' | 'Salida' | 'Ajuste'
  cantidad: m.cantidad,
  balance: m.balance,
  referenciaCod: m.referenciaCod ?? '',
  notas: m.notas ?? '',
  usuarioNombre: m.usuarioNombre,
  creadoEn: m.creadoEn,
});

/**
 * Lista TODOS los artículos (activos e inactivos), para el panel de admin.
 */
export const getArticulosAdmin = async () => {
  try {
    const { data } = await api.get('/articulos', { params: { soloActivos: false } });
    return data.map(mapArticulo);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los artículos'));
  }
};

export const createArticulo = async ({ nombre, descripcion, precio, tipo, categoriaId, stockInicial }) => {
  try {
    const { data } = await api.post('/articulos', {
      nombre, descripcion: descripcion || null, precio, tipo, categoriaId, stockInicial,
    });
    return mapArticulo(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear el artículo'));
  }
};

export const updateArticulo = async (id, { nombre, descripcion, precio, tipo, categoriaId, activo }) => {
  try {
    const { data } = await api.put(`/articulos/${id}`, {
      nombre, descripcion: descripcion || null, precio, tipo, categoriaId, activo,
    });
    return mapArticulo(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar el artículo'));
  }
};

/**
 * Desactiva un artículo (borrado lógico). Para reactivarlo, usa
 * updateArticulo(id, { ..., activo: true }).
 */
export const deleteArticulo = async (id) => {
  try {
    await api.delete(`/articulos/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar el artículo'));
  }
};

/**
 * Historial de movimientos de stock del artículo (más reciente primero).
 */
export const getMovimientosArticulo = async (articuloId) => {
  try {
    const { data } = await api.get(`/articulos/${articuloId}/movimientos`);
    return data.map(mapMovimiento);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo cargar el historial de movimientos'));
  }
};

/**
 * Registra un movimiento de stock.
 * @param {number} articuloId
 * @param {{ tipoMovimiento: 'Entrada'|'Salida'|'Ajuste', cantidad: number, referenciaCod?: string, notas?: string }} payload
 */
export const registrarMovimiento = async (articuloId, { tipoMovimiento, cantidad, referenciaCod, notas }) => {
  try {
    const { data } = await api.post(`/articulos/${articuloId}/movimientos`, {
      tipoMovimiento,
      cantidad,
      referenciaCod: referenciaCod || null,
      notas: notas || null,
    });
    return mapMovimiento(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo registrar el movimiento'));
  }
};
