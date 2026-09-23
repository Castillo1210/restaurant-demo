// ============================================================
//  jwt.js — Utilidad mínima para leer el payload de un JWT
//  Solo se usa para extraer el "usuarioId" (necesario para llamar
//  a endpoints como /usuarios/{id}/cambiar-password), ya que
//  LoginResponse no lo incluye explícitamente. NO valida la firma
//  (eso lo hace el backend en cada request); es solo lectura local.
// ============================================================

export const decodeJwtPayload = (token) => {
  try {
    const base64Url = token.split('.')[1];
    if (!base64Url) return null;
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
        .join('')
    );
    return JSON.parse(json);
  } catch (e) {
    console.error('No se pudo decodificar el token JWT:', e);
    return null;
  }
};

/**
 * Extrae el UsuarioId numérico desde el claim personalizado "usuarioId"
 * que el backend agrega al token (ver JwtService.cs).
 */
export const getUsuarioIdFromToken = (token) => {
  const payload = decodeJwtPayload(token);
  const raw = payload?.usuarioId;
  const id = parseInt(raw, 10);
  return Number.isNaN(id) ? null : id;
};
