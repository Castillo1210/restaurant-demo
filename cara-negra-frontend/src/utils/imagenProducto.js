// ============================================================
//  imagenProducto.js — Fotos de los productos
//  La foto se reduce y comprime EN EL NAVEGADOR antes de subirla:
//  una foto de celular (3-6 MB) queda en ~30-80 KB, así carga rápido
//  en los celulares de los mozos aunque el Wi-Fi esté cargado.
// ============================================================

import { API_BASE_URL } from '../services/api';

/** Lado mayor de la foto guardada (suficiente para la vista ampliada en un celular). */
export const LADO_MAXIMO = 800;
const CALIDAD = 0.82;

/**
 * URL de la foto (o null si no tiene). La versión va en la URL para que el navegador la guarde
 * en caché y la vuelva a descargar solo cuando se cambie.
 */
export const urlImagenProducto = (productoId, version) =>
  version > 0 ? `${API_BASE_URL}/productos/${productoId}/imagen?v=${version}` : null;

const cargarImagen = (archivo) =>
  new Promise((resolve, reject) => {
    const url = URL.createObjectURL(archivo);
    const img = new Image();
    img.onload = () => { URL.revokeObjectURL(url); resolve(img); };
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('No se pudo leer la imagen. Usa una foto JPG, PNG o WebP.')); };
    img.src = url;
  });

const aBlob = (canvas, tipo) =>
  new Promise((resolve) => canvas.toBlob((b) => resolve(b), tipo, CALIDAD));

/**
 * Reduce la foto a máx. 800 px y la comprime (WebP, o JPEG si el navegador no sabe WebP).
 * @param {File} archivo
 * @returns {Promise<Blob>}
 */
export const comprimirImagen = async (archivo) => {
  if (!archivo || !archivo.type?.startsWith('image/')) {
    throw new Error('El archivo elegido no es una imagen');
  }
  const img = await cargarImagen(archivo);
  const escala = Math.min(1, LADO_MAXIMO / Math.max(img.naturalWidth, img.naturalHeight));
  const ancho = Math.max(1, Math.round(img.naturalWidth * escala));
  const alto = Math.max(1, Math.round(img.naturalHeight * escala));

  const canvas = document.createElement('canvas');
  canvas.width = ancho;
  canvas.height = alto;
  const ctx = canvas.getContext('2d');
  // Fondo blanco: una PNG con transparencia no queda con fondo negro al pasar a JPEG.
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, ancho, alto);
  ctx.drawImage(img, 0, 0, ancho, alto);

  let blob = await aBlob(canvas, 'image/webp');
  if (!blob || blob.type !== 'image/webp') blob = await aBlob(canvas, 'image/jpeg');
  if (!blob) throw new Error('No se pudo procesar la imagen');
  return blob;
};
