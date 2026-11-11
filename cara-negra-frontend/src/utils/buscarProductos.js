// ============================================================
//  buscarProductos.js — Buscador de la carta (en el navegador)
//  La carta completa ya está cargada (unos 60-100 productos), así
//  que se filtra al instante mientras el mozo escribe, sin esperar
//  al servidor. Ignora tildes y mayúsculas ("aji" encuentra "Ají de
//  gallina") y acepta varias palabras en cualquier orden ("chicha
//  jarra" encuentra "Jarra de chicha morada").
// ============================================================

/** Minúsculas y sin tildes/diéresis ("Ají" → "aji", "Piña" → "pina"). */
export const normalizar = (texto) =>
  String(texto ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase();

const palabrasDe = (texto) => normalizar(texto).split(/[^a-z0-9]+/).filter(Boolean);

/**
 * Puntaje de un producto para una palabra buscada (menor = mejor), o null si no aparece.
 *   0 el nombre empieza con la palabra · 1 alguna palabra del nombre empieza con ella
 *   2 el nombre la contiene · 3 la categoría · 4 la descripción
 */
const puntajePalabra = (prep, palabra) => {
  if (prep.nombre.startsWith(palabra)) return 0;
  if (prep.palabrasNombre.some((p) => p.startsWith(palabra))) return 1;
  if (prep.nombre.includes(palabra)) return 2;
  if (prep.categoria.includes(palabra)) return 3;
  if (prep.descripcion.includes(palabra)) return 4;
  return null;
};

/**
 * Prepara la carta para buscar rápido (se llama una vez al cargarla).
 * @param {Array<{name:string, description?:string, categoryName?:string}>} productos
 */
export const prepararIndice = (productos) =>
  productos.map((producto) => ({
    producto,
    nombre: normalizar(producto.name),
    palabrasNombre: palabrasDe(producto.name),
    categoria: normalizar(producto.categoryName),
    descripcion: normalizar(producto.description),
  }));

/**
 * Busca en TODA la carta, sin importar la categoría.
 * @param {ReturnType<typeof prepararIndice>} indice
 * @param {string} consulta lo que va escribiendo el mozo
 * @param {number} [limite]
 * @returns {Array} productos ordenados del más al menos parecido
 */
export const buscarEnIndice = (indice, consulta, limite = 50) => {
  const palabras = palabrasDe(consulta);
  if (palabras.length === 0) return [];

  const encontrados = [];
  for (const prep of indice) {
    let total = 0;
    let coincide = true;
    for (const palabra of palabras) {
      const p = puntajePalabra(prep, palabra);
      if (p === null) { coincide = false; break; }
      total += p;
    }
    if (coincide) encontrados.push({ prep, total });
  }

  encontrados.sort((a, b) => a.total - b.total || a.prep.nombre.localeCompare(b.prep.nombre));
  return encontrados.slice(0, limite).map((e) => e.prep.producto);
};

/**
 * Parte el nombre en trozos para resaltar lo buscado:
 * [{ texto: 'Ají de ', resaltar: false }, { texto: 'gall', resaltar: true }, ...]
 */
export const resaltarCoincidencias = (nombre, consulta) => {
  const original = String(nombre ?? '');
  const palabras = palabrasDe(consulta);
  // Se normaliza letra por letra para que las posiciones coincidan con el texto original.
  const letras = [...original].map((c) => normalizar(c));
  if (palabras.length === 0 || letras.some((l) => l.length !== 1)) return [{ texto: original, resaltar: false }];

  const plano = letras.join('');
  const marcar = new Array(letras.length).fill(false);
  for (const palabra of palabras) {
    let desde = 0;
    for (;;) {
      const i = plano.indexOf(palabra, desde);
      if (i < 0) break;
      for (let k = i; k < i + palabra.length; k++) marcar[k] = true;
      desde = i + palabra.length;
    }
  }

  const caracteres = [...original];
  const trozos = [];
  caracteres.forEach((c, i) => {
    const ultimo = trozos[trozos.length - 1];
    if (ultimo && ultimo.resaltar === marcar[i]) ultimo.texto += c;
    else trozos.push({ texto: c, resaltar: marcar[i] });
  });
  return trozos;
};
