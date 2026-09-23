import { createContext, useState, useContext, useEffect, useCallback } from 'react';

const CartContext = createContext();

export function CartProvider({ children }) {
  const [cart, setCart] = useState(() => {
    try {
      const savedCart = localStorage.getItem('caraNegraCart');
      if (savedCart) {
        const parsed = JSON.parse(savedCart);
        return Array.isArray(parsed) ? parsed : [];
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
          setCart(Array.isArray(newVal) ? newVal : []);
        } catch (err) {
          console.error('Storage sync error:', err);
        }
      }
    };
    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, []);

  const addToCart = useCallback((item) => {
    setCart(prev => {
      const existing = prev.find(i => i.id === item.id);
      if (existing) {
        return prev.map(i => i.id === item.id ? { ...i, quantity: i.quantity + 1 } : i);
      }
      return [...prev, { ...item, quantity: 1 }];
    });
  }, []);

  const removeFromCart = useCallback((id) => {
    setCart(prev => prev.filter(i => i.id !== id));
  }, []);
  
  const updateQuantity = useCallback((id, delta) => {
    setCart(prev =>
      prev
        .map(i => {
          if (i.id === id) {
            return { ...i, quantity: i.quantity + delta };
          }
          return i;
        })
        .filter(i => i.quantity > 0)
    );
  }, []);

  const clearCart = useCallback(() => {
    setCart([]);
    localStorage.removeItem('caraNegraCart');
  }, []);

  const total = cart.reduce((acc, i) => acc + (i.price * i.quantity), 0);

  const openCart = useCallback(() => setCartOpen(true), []);
  const closeCart = useCallback(() => setCartOpen(false), []);

  return (
    <CartContext.Provider value={{ 
      cart, activeTable, setActiveTable, 
      addToCart, removeFromCart, updateQuantity, clearCart, total,
      cartOpen, openCart, closeCart
    }}>
      {children}
    </CartContext.Provider>
  );
}

export const useCart = () => useContext(CartContext);