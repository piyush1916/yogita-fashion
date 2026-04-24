import { createContext, useContext, useEffect, useState } from "react";
import authService from "../services/authService";

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(authService.profile());

  useEffect(() => {
    if (!user?.id || user?.createdAt) return undefined;

    let ignore = false;
    const hydrateProfile = async () => {
      try {
        const nextUser = await authService.getProfile(user.id);
        if (!ignore && nextUser) {
          setUser(nextUser);
        }
      } catch {
        // Keep the current session if profile hydration fails.
      }
    };

    hydrateProfile();
    return () => {
      ignore = true;
    };
  }, [user?.id, user?.createdAt]);

  const login = async (credentials) => {
    const u = await authService.login(credentials);
    setUser(u);
    return u;
  };

  const register = async (data) => {
    const u = await authService.register(data);
    setUser(u);
    return u;
  };

  const updateProfile = async (data) => {
    const u = await authService.updateProfile(data);
    setUser(u);
    return u;
  };

  const logout = () => {
    authService.logout();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, login, register, updateProfile, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
};
