// ============================================================
//  menuService.js — Servicio del menú (backend real)
//  Consume GET /categorias y GET /productos. El catálogo puede
//  estar vacío hasta que el administrador cargue productos desde
//  el panel (Fase 3) — este servicio no debe rellenar datos falsos.
// ============================================================

import api, { getApiErrorMessage } from './api';
import { getCategoryIcon } from './categoryIcons';

const mapCategoria = (c) => ({
  id: c.categoriaId,
  label: c.nombre,
  icon: getCategoryIcon(c.nombre),
});

const mapProducto = (p) => ({
  id: p.productoId,
  category: p.categoriaId,
  categoryName: p.categoriaNombre,
  name: p.nombre,
  description: p.descripcion ?? '',
  price: p.precio,
  disponible: p.estaDisponible,
  tipo: p.tipo,
  // El catálogo aún no maneja imágenes de producto; se usa un
  // placeholder neutro hasta que se agregue esa capacidad.
  image: null,
});

/**
 * Obtiene todas las categorías activas del menú.
 * @returns {Promise<Array<{id: number, label: string, icon: string}>>}
 */
export const getCategories = async () => {
  try {
    const { data } = await api.get('/categorias', { params: { soloActivas: true } });
    return data.map(mapCategoria);
  } catch (error) {
    console.error('Error al cargar categorías:', getApiErrorMessage(error));
    return [];
  }
};

/**
 * Obtiene todos los productos disponibles.
 * @returns {Promise<Array>}
 */
export const getMenuItems = async () => {
  try {
    const { data } = await api.get('/productos', { params: { soloDisponibles: true } });
    return data.map(mapProducto);
  } catch (error) {
    console.error('Error al cargar productos:', getApiErrorMessage(error));
    return [];
  }
};

/**
 * Obtiene productos filtrados por categoría.
 * @param {number} categoriaId
 * @returns {Promise<Array>}
 */
export const getItemsByCategory = async (categoriaId) => {
  try {
    const { data } = await api.get('/productos', {
      params: { soloDisponibles: true, categoriaId },
    });
    return data.map(mapProducto);
  } catch (error) {
    console.error('Error al cargar productos de la categoría:', getApiErrorMessage(error));
    return [];
  }
};

/**
 * Busca productos por nombre o descripción (filtro en el cliente,
 * ya que el backend aún no expone un endpoint de búsqueda de texto).
 * @param {string} query
 * @returns {Promise<Array>}
 */
export const searchMenuItems = async (query) => {
  const items = await getMenuItems();
  const q = query.toLowerCase();
  return items.filter(
    (item) =>
      item.name.toLowerCase().includes(q) ||
      item.description.toLowerCase().includes(q)
  );
};
