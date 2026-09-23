// ============================================================
//  adminMenuService.js — CRUD de categorías y productos (backend real)
//  Usado por el panel de administración de carta/menú (Fase 3), a
//  diferencia de menuService.js (que es de solo lectura y siempre
//  filtra "activos/disponibles" porque lo consume el mozo). Aquí el
//  administrador necesita ver también los inactivos/no disponibles
//  para poder reactivarlos.
// ============================================================

import api, { getApiErrorMessage } from './api';

const mapCategoria = (c) => ({
  id: c.categoriaId,
  nombre: c.nombre,
  descripcion: c.descripcion ?? '',
  estaActivo: c.estaActivo,
  creadoEn: c.creadoEn,
});

const mapProducto = (p) => ({
  id: p.productoId,
  nombre: p.nombre,
  descripcion: p.descripcion ?? '',
  precio: p.precio,
  estaDisponible: p.estaDisponible,
  tipo: p.tipo,
  categoriaId: p.categoriaId,
  categoriaNombre: p.categoriaNombre,
  creadoEn: p.creadoEn,
});

// ─── Categorías ─────────────────────────────────────────────────────────────

/**
 * Lista TODAS las categorías (activas e inactivas), para el panel de admin.
 */
export const getCategoriasAdmin = async () => {
  try {
    const { data } = await api.get('/categorias', { params: { soloActivas: false } });
    return data.map(mapCategoria);
  } catch (error) {
    console.error('Error al cargar categorías:', getApiErrorMessage(error));
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar las categorías'));
  }
};

export const createCategoria = async ({ nombre, descripcion }) => {
  try {
    const { data } = await api.post('/categorias', { nombre, descripcion: descripcion || '' });
    return mapCategoria(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear la categoría'));
  }
};

export const updateCategoria = async (id, { nombre, descripcion, estaActivo }) => {
  try {
    const { data } = await api.put(`/categorias/${id}`, {
      nombre,
      descripcion: descripcion || '',
      estaActivo,
    });
    return mapCategoria(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar la categoría'));
  }
};

/**
 * Desactiva una categoría (borrado lógico). Para reactivarla, usa
 * updateCategoria(id, { ..., estaActivo: true }).
 */
export const deleteCategoria = async (id) => {
  try {
    await api.delete(`/categorias/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar la categoría'));
  }
};

// ─── Productos ──────────────────────────────────────────────────────────────

/**
 * Lista TODOS los productos (disponibles y no disponibles), para el panel de admin.
 */
export const getProductosAdmin = async () => {
  try {
    const { data } = await api.get('/productos', { params: { soloDisponibles: false } });
    return data.map(mapProducto);
  } catch (error) {
    console.error('Error al cargar productos:', getApiErrorMessage(error));
    throw new Error(getApiErrorMessage(error, 'No se pudieron cargar los productos'));
  }
};

export const createProducto = async ({ nombre, descripcion, precio, tipo, categoriaId }) => {
  try {
    const { data } = await api.post('/productos', {
      nombre,
      descripcion: descripcion || null,
      precio,
      tipo,
      categoriaId,
    });
    return mapProducto(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo crear el producto'));
  }
};

export const updateProducto = async (id, { nombre, descripcion, precio, tipo, categoriaId, estaDisponible }) => {
  try {
    const { data } = await api.put(`/productos/${id}`, {
      nombre,
      descripcion: descripcion || null,
      precio,
      tipo,
      categoriaId,
      estaDisponible,
    });
    return mapProducto(data);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo actualizar el producto'));
  }
};

/**
 * Marca un producto como no disponible (borrado lógico). Para reactivarlo,
 * usa updateProducto(id, { ..., estaDisponible: true }).
 */
export const deleteProducto = async (id) => {
  try {
    await api.delete(`/productos/${id}`);
  } catch (error) {
    throw new Error(getApiErrorMessage(error, 'No se pudo desactivar el producto'));
  }
};
