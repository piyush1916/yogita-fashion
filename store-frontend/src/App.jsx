import { BrowserRouter } from "react-router-dom";
import { ToastProvider } from "./hooks/useToast";
import { CartProvider } from "./context/CartContext";
import { WishlistProvider } from "./context/WishlistContext";
import { AuthProvider } from "./context/AuthContext";
import Header from "./components/layout/Header";
import PromoBanner from "./components/layout/PromoBanner";
import Footer from "./components/layout/Footer";
import WhatsAppFloat from "./components/layout/WhatsAppFloat";
import AppRoutes from "./routes/AppRoutes";

function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <WishlistProvider>
          <CartProvider>
            <BrowserRouter>
              <div className="flex flex-col min-h-screen">
                <Header />
                <PromoBanner />
                <main className="flex-grow">
                  <AppRoutes />
                </main>
                <Footer />
                <WhatsAppFloat />
              </div>
            </BrowserRouter>
          </CartProvider>
        </WishlistProvider>
      </AuthProvider>
    </ToastProvider>
  );
}

export default App;
