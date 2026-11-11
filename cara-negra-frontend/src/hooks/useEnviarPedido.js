import { useState } from 'react';
import { useCart } from '../CartContext';
import { useAuth } from '../context/AuthContext';
import { createPedido, agregarDetalles, getActivePedidoForMesa } from '../services/ordersService';
import useAccionUnica from './useAccionUnica';
import { useEmpresa } from '../context/EmpresaContext';
import { imprimirComandaEnCaja } from '../utils/comandaCaja';

// Administrador y cajero: su comanda no sale en cocina, la imprime este mismo equipo en su
// ticketera (impresora predeterminada de Windows), igual que la boleta.
export const IMPRIMEN_SU_COMANDA = ['ADMIN', 'CAJERO'];

/**
 * Envía a cocina lo que hay en "Por enviar" de la mesa activa. Es la misma lógica para la
 * pantalla "Revisar pedido" y para el botón de un solo clic del monitor:
 *   · mesa libre → crea el pedido; mesa ocupada → agrega todo al pedido abierto en UNA petición
 *     (cocina recibe una sola comanda ADICIONAL y, si se corta el Wi-Fi, no quedan platos a medias);
 *   · con el código de operación del carrito, un reintento no duplica el pedido;
 *   · anti doble clic (el servidor además bloquea la mesa / el pedido);
 *   · administrador y cajero: la comanda se imprime aquí mismo, en la ticketera de este equipo.
 * Si sale bien vacía el carrito y devuelve { pedido, comandaCaja }; si falla lanza el error
 * (el carrito queda intacto para reintentar).
 */
export default function useEnviarPedido() {
  const { cart, activeTable, clearCart, obtenerOperacionId } = useCart();
  const { user } = useAuth();
  const { nombreComercial } = useEmpresa();
  const [enviando, setEnviando] = useState(false);
  const unaVez = useAccionUnica();

  /**
   * @param {{ comentario?: string, comentarioOriginal?: string|null }} [opciones]
   *   comentario: comentario general del pedido (solo desde "Revisar pedido").
   *   comentarioOriginal: el que ya tenía el pedido; si no cambió no se envía (no se pisa el de otro mozo).
   *   Sin `comentario`, el comentario del pedido no se toca.
   */
  const enviar = (opciones = {}) => unaVez(async () => {
    if (!activeTable) throw new Error('No hay una mesa seleccionada');
    if (!user?.usuarioId) throw new Error('No se pudo identificar al mozo. Vuelve a iniciar sesión.');
    if (cart.length === 0) throw new Error('No hay nada por enviar');

    const { comentario, comentarioOriginal = null } = opciones;
    setEnviando(true);
    try {
      const detalles = cart.map((item) => ({
        productoId: item.id,
        cantidad: item.quantity,
        // Nota de este plato ("sin cebolla"): sale debajo del plato en la comanda.
        notas: item.nota?.trim() || undefined,
        // Precio cambiado por el cajero / admin (el servidor lo valida y lo registra).
        precioUnitario: item.precioEditado ? item.price : undefined,
      }));
      const operacionId = obtenerOperacionId();
      const esAdicional = activeTable.status === 'occupied';
      let pedido;

      if (esAdicional) {
        const activo = await getActivePedidoForMesa(activeTable.id);
        if (!activo) {
          throw new Error('La mesa figura ocupada pero no se encontró su pedido activo. Actualiza la pantalla e intenta de nuevo.');
        }
        // El comentario solo se envía si cambió. Si no se pudo leer el actual, solo se envía lo escrito.
        const cambioComentario = comentario === undefined
          ? undefined
          : comentarioOriginal === null
            ? (comentario || undefined)
            : (comentario !== comentarioOriginal.trim() ? comentario : undefined);
        pedido = await agregarDetalles(activo.id, detalles, cambioComentario, operacionId);
      } else {
        pedido = await createPedido({
          mesaId: activeTable.id,
          usuarioId: user.usuarioId,
          detalles,
          comentario: comentario ?? '',
          operacionId,
        });
      }

      // Lo enviado, para que el cajero pueda imprimir la comanda también en la ticketera de caja.
      const comandaCaja = {
        pedidoId: pedido.id,
        mesaNumero: pedido.mesaNumero,
        salonNombre: pedido.salonNombre,
        atendidoPor: user.nombreCompleto,
        esAdicional,
        fecha: new Date().toISOString(),
        comentario: pedido.comentario || comentario || '',
        items: cart.map((item) => ({ cantidad: item.quantity, nombre: item.name, notas: item.nota })),
      };

      clearCart();
      if (IMPRIMEN_SU_COMANDA.includes(user.rol)) {
        // Como la boleta: sale en la ticketera de este equipo (sin ventana si Chrome tiene --kiosk-printing).
        imprimirComandaEnCaja({ ...comandaCaja, nombreComercial }).catch((e) => console.error('No se pudo imprimir la comanda:', e));
      }
      return { pedido, comandaCaja };
    } finally {
      setEnviando(false);
    }
  });

  return { enviar, enviando };
}
