// ============================================================
//  clientesService.js — Consulta de cliente por documento
//  El backend busca primero en la base local de clientes y,
//  solo si el cliente es nuevo, en la API de DNI (RENIEC) /
//  RUC (SUNAT); el resultado queda guardado para la próxima vez.
// ============================================================

import api, { getApiErrorMessage } from './api';

/**
 * @param {'DNI'|'RUC'|'CarnetExtranjeria'} tipo
 * @param {string} numero
 * @returns {Promise<{ estado: 'encontrado'|'no_encontrado'|'no_disponible'|'error', cliente?: object, mensaje?: string }>}
 */
export const consultarDocumento = async (tipo, numero) => {
  try {
    const { data } = await api.get('/clientes/consulta', { params: { tipo, numero } });
    return { estado: 'encontrado', cliente: data };
  } catch (error) {
    const status = error?.response?.status;
    const mensaje = getApiErrorMessage(error, 'No se pudo consultar el documento');
    if (status === 404) return { estado: 'no_encontrado', mensaje };
    if (status === 503) return { estado: 'no_disponible', mensaje };
    return { estado: 'error', mensaje };
  }
};
