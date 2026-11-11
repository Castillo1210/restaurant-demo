import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { CartProvider } from './CartContext';
import { AuthProvider } from './context/AuthContext';
import { EmpresaProvider } from './context/EmpresaContext';
import AdminEmpresa from './pages/AdminEmpresa';
import RequireAuth from './components/RequireAuth';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import CartPage from './pages/CartPage';
import OrderSuccess from './pages/OrderSuccess';
import Caja from './pages/Caja';
import AdminHub from './pages/AdminHub';
import AdminMenu from './pages/AdminMenu';
import AdminMesas from './pages/AdminMesas';
import AdminSalones from './pages/AdminSalones';
import AdminReportes from './pages/AdminReportes';
import AdminUsuarios from './pages/AdminUsuarios';
import AdminInventario from './pages/AdminInventario';
import AdminDescuentos from './pages/AdminDescuentos';
import AdminCremas from './pages/AdminCremas';
import Cocina from './pages/Cocina';
import Comprobantes from './pages/Comprobantes';
import PedidosDia from './pages/PedidosDia';
import AdminDispositivos from './pages/AdminDispositivos';
import Jornada from './pages/Jornada';
import JornadaGate from './components/JornadaGate';
import { JornadaProvider } from './context/JornadaContext';
import CierreInactividad from './components/CierreInactividad';
import AvisoImpresora from './components/AvisoImpresora';

function App() {
  return (
    <Router>
      <EmpresaProvider>
      <AuthProvider>
        <JornadaProvider>
        <CartProvider>
          <CierreInactividad />
          <JornadaGate />
          <AvisoImpresora />
          <Routes>
            <Route path="/" element={<Navigate to="/login" />} />
            <Route path="/login" element={<Login />} />
            <Route
              path="/dashboard"
              element={
                <RequireAuth>
                  <Dashboard />
                </RequireAuth>
              }
            />
            <Route
              path="/cart"
              element={
                <RequireAuth>
                  <CartPage />
                </RequireAuth>
              }
            />
            <Route
              path="/success"
              element={
                <RequireAuth>
                  <OrderSuccess />
                </RequireAuth>
              }
            />
            <Route
              path="/caja"
              element={
                <RequireAuth roles={['CAJERO', 'ADMIN']}>
                  <Caja />
                </RequireAuth>
              }
            />
            <Route
              path="/cocina"
              element={
                <RequireAuth roles={['COCINA', 'ADMIN']}>
                  <Cocina />
                </RequireAuth>
              }
            />
            <Route
              path="/comprobantes"
              element={
                <RequireAuth roles={['CAJERO', 'ADMIN']}>
                  <Comprobantes />
                </RequireAuth>
              }
            />
            <Route
              path="/pedidos-dia"
              element={
                <RequireAuth roles={['CAJERO', 'ADMIN']}>
                  <PedidosDia />
                </RequireAuth>
              }
            />
            <Route
              path="/admin"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminHub />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/menu"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminMenu />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/empresa"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminEmpresa />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/salones"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminSalones />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/mesas"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminMesas />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/reportes"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminReportes />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/usuarios"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminUsuarios />
                </RequireAuth>
              }
            />
            <Route
              path="/jornada"
              element={
                <RequireAuth roles={['ADMIN', 'CAJERO']}>
                  <Jornada />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/dispositivos"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminDispositivos />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/inventario"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminInventario />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/descuentos"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminDescuentos />
                </RequireAuth>
              }
            />
            <Route
              path="/admin/cremas"
              element={
                <RequireAuth roles={['ADMIN']}>
                  <AdminCremas />
                </RequireAuth>
              }
            />
          </Routes>

          <Toaster
            position="top-center"
            gutter={8}
            toastOptions={{
              duration: 2500,
              style: {
                // Colores del tema (claro / oscuro), ver src/tema.css
                background: 'rgb(var(--c-card))',
                color: 'rgb(var(--c-tinta))',
                border: '1px solid rgb(var(--gray-800) / 0.6)',
                boxShadow: '0 8px 24px rgb(0 0 0 / 0.15)',
                borderRadius: '14px',
                fontSize: '14px',
                fontFamily: 'Inter, sans-serif',
                padding: '12px 16px',
              },
              success: {
                iconTheme: { primary: '#10b981', secondary: '#fff' },
              },
              error: {
                iconTheme: { primary: '#ef4444', secondary: '#fff' },
              },
            }}
          />
        </CartProvider>
        </JornadaProvider>
      </AuthProvider>
      </EmpresaProvider>
    </Router>
  );
}

export default App;