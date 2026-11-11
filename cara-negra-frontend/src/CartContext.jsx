import { createContext, useState, useContext, useEffect, useCallback } from 'react';
import { useAuth } from './context/AuthContext';

const CartContext = createContext();

// Cada línea del carrito tiene su propia clave (`linea`): el mismo plato puede ir en dos líneas
// con notas distintas ("1 Lomo saltado sin cebolla" + "1 Lomo saltado"). `id` sigue siendo el
// producto. Los carritos guardados antes de las notas por plato se completan al cargar.
const nuevaLinea = (productoId) => `${productoId}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 7)}`;
const conLinea = (i) => (i && i.linea ? i : { ...i, linea: String(i?.id) });
// Dos líneas del mismo plato, con la misma nota y el mismo precio, se juntan en una sola.
const mismaLinea = (a, b) =>
  a.id === b.id && (a.nota || '') === (b.nota || '') && Number(a.price) === Number(b.price) && !!a.precioEditado === !!b.precioEditado;
// Código único de cada envío a cocina (se manda al servidor). Si el Wi-Fi corta la respuesta y
// el mozo vuelve a tocar "Enviar" con el MISMO carrito, viaja el mismo código y el servidor no
// duplica los platos. Si el carrito cambió, es otro envío y lleva código nuevo.
// (crypto.randomUUID no existe en http sin TLS, por eso el respaldo.)
const nuevoCodigo = () =>
  (globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`);
const firmaCarrito = (lineas) =>
  JSON.stringify(lineas.map((i) => [i.id, i.quantity, i.nota || '', Number(i.price)]));
const CLAVE_OPERACION = 'caraNegraCartOp';

const juntar = (lineas) => {
  const out = [];
  for (const l of lineas) {
    const igual = out.find((o) => mismaLinea(o, l));
    if (igual) igual.quantity += l.quantity;
    else out.push({ ...l });
  }
  return out;
};

export function CartProvider({ children }) {
  const [cart, setCart] = useState(() => {
    try {
      const savedCart = localStorage.getItem('caraNegraCart');
      if (savedCart) {
        const parsed = JSON.parse(savedCart);
        return Array.isArray(parsed) ? parsed.map(conLinea) : [];
      }
    } catch (e) {
      console.error('Error parsing cart from localStorage:', e);
    }
    return [];
  });

  const [activeTable, setActiveTable] = useState(() => {
    try {
      const savedTable = localStorage.getItem('caraNegraTable');
      if (savedTable) {
        return JSON.parse(savedTable);
      }
    } catch (e) {
      console.error('Error parsing table from localStorage:', e);
    }
    return null;
  });

  const [cartOpen, setCartOpen] = useState(false);

  // El pedido en curso pertenece al mozo que lo empezó. En una tablet compartida (ingreso con
  // PIN), si entra OTRO mozo no debe ver ni enviar el carrito del anterior; si vuelve a entrar el
  // mismo, lo recupera.
  const { user } = useAuth();
  const usuarioId = user?.usuarioId ?? null;
  useEffect(() => {
    if (usuarioId === null) return;
    try {
      const dueno = localStorage.getItem('caraNegraCartOwner');
      if (dueno !== null && dueno !== String(usuarioId)) {
        setCart([]);
        setActiveTable(null);
      }
      localStorage.setItem('caraNegraCartOwner', String(usuarioId));
    } catch (e) {
      console.error('Error leyendo el dueño del carrito:', e);
    }
  }, [usuarioId]);

  useEffect(() => {
    try {
      localStorage.setItem('caraNegraCart', JSON.stringify(cart));
    } catch (e) {
      console.error('Error saving cart to localStorage:', e);
    }
  }, [cart]);

  useEffect(() => {
    try {
      localStorage.setItem('caraNegraTable', JSON.stringify(activeTable));
    } catch (e) {
      console.error('Error saving table to localStorage:', e);
    }
  }, [activeTable]);

  // Sincronización entre pestañas o contextos desincronizados
  useEffect(() => {
    const handleStorage = (e) => {
      if (e.key === 'caraNegraCart') {
        try {
          const newVal = e.newValue ? JSON.parse(e.newValue) : [];
          setCart(Array.isArray(newVal) ? newVal.map(conLinea) : []);
        } catch (err) {
          console.error('Storage sync error:', err);
        }
      }
    };
    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, []);

  // Tocar un plato en la carta suma 1 a su línea SIN nota (las que tienen nota quedan aparte).
  const addToCart = useCallback((item) => {
    setCart(prev => {
      const existing = prev.find(i => i.id === item.id && !i.nota);
      if (existing) {
        return prev.map(i => i.linea === existing.linea ? { ...i, quantity: i.quantity + 1 } : i);
      }
      return [...prev, { ...item, quantity: 1, linea: nuevaLinea(item.id), nota: '' }];
    });
  }, []);

  /** Quita una línea completa (por su clave `linea`). */
  const removeFromCart = useCallback((linea) => {
    setCart(prev => prev.filter(i => i.linea !== linea));
  }, []);

  /** Suma o resta unidades a una línea (por su clave `linea`); en 0 la línea desaparece. */
  const updateQuantity = useCallback((linea, delta) => {
    setCart(prev =>
      prev
        .map(i => (i.linea === linea ? { ...i, quantity: i.quantity + delta } : i))
        .filter(i => i.quantity > 0)
    );
  }, []);

  /** "−" en la tarjeta de la carta: quita una unidad del plato, primero de la línea sin nota. */
  const quitarUnoDelProducto = useCallback((productoId) => {
    setCart(prev => {
      const lineas = prev.filter(i => i.id === productoId);
      if (lineas.length === 0) return prev;
      const objetivo = lineas.find(i => !i.nota) ?? lineas[lineas.length - 1];
      return prev
        .map(i => (i.linea === objetivo.linea ? { ...i, quantity: i.quantity - 1 } : i))
        .filter(i => i.quantity > 0);
    });
  }, []);

  // Precio distinto al de la carta (solo cajero / admin; el servidor lo vuelve a validar y lo
  // registra). Se guarda el precio de carta para mostrar el cambio.
  const setItemPrice = useCallback((linea, price) => {
    setCart(prev => juntar(prev.map(i => {
      if (i.linea !== linea) return i;
      const precioCarta = i.precioCarta ?? i.price;
      return { ...i, price, precioCarta, precioEditado: Number(price) !== Number(precioCarta) };
    })));
  }, []);

  /**
   * Nota de un plato ("sin cebolla"). Con `cantidad` menor a la de la línea, solo esas unidades
   * llevan la nota: se separan en una línea nueva (p. ej. de 3 lomos, 1 "sin cebolla").
   */
  const setItemNote = useCallback((linea, nota, cantidad) => {
    const texto = String(nota ?? '').replace(/\s+/g, ' ').trim();
    setCart(prev => {
      const actual = prev.find(i => i.linea === linea);
      if (!actual) return prev;
      const n = Math.min(Math.max(1, Number(cantidad) || actual.quantity), actual.quantity);
      if (n >= actual.quantity) {
        return juntar(prev.map(i => (i.linea === linea ? { ...i, nota: texto } : i)));
      }
      const resto = prev.map(i => (i.linea === linea ? { ...i, quantity: i.quantity - n } : i));
      const idx = resto.findIndex(i => i.linea === linea);
      const separada = { ...actual, quantity: n, nota: texto, linea: nuevaLinea(actual.id) };
      return juntar([...resto.slice(0, idx + 1), separada, ...resto.slice(idx + 1)]);
    });
  }, []);

  const clearCart = useCallback(() => {
    setCart([]);
    localStorage.removeItem('caraNegraCart');
    try { localStorage.removeItem(CLAVE_OPERACION); } catch { /* sin almacenamiento */ }
  }, []);

  /** Código del envío actual: el mismo mientras el carrito no cambie (ver nuevoCodigo). */
  const obtenerOperacionId = useCallback(() => {
    const firma = firmaCarrito(cart);
    try {
      const guardada = JSON.parse(localStorage.getItem(CLAVE_OPERACION) || 'null');
      if (guardada?.firma === firma && guardada?.id) return guardada.id;
      const id = nuevoCodigo();
      localStorage.setItem(CLAVE_OPERACION, JSON.stringify({ id, firma }));
      return id;
    } catch {
      return nuevoCodigo();
    }
  }, [cart]);

  const total = cart.reduce((acc, i) => acc + (i.price * i.quantity), 0);

  const openCart = useCallback(() => setCartOpen(true), []);
  const closeCart = useCallback(() => setCartOpen(false), []);

  return (
    <CartContext.Provider value={{ 
      cart, activeTable, setActiveTable, 
      addToCart, removeFromCart, updateQuantity, quitarUnoDelProducto, setItemPrice, setItemNote, clearCart, total,
      obtenerOperacionId,
      cartOpen, openCart, closeCart
    }}>
      {children}
    </CartContext.Provider>
  );
}

export const useCart = () => useContext(CartContext);