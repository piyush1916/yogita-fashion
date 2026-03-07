import { createContext, useContext, useMemo, useState } from "react";
import { getAdminSession, loginAdmin, logoutAdmin } from "../services/authService";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [adminUser, setAdminUser] = useState(() => getAdminSession());

  const value = useMemo(
    () => ({
      adminUser,
      isAuthenticated: Boolean(adminUser),
      login: async (credentials) => {
        const user = await loginAdmin(credentials);
        setAdminUser(user);
        return user;
      },
      logout: () => {
        logoutAdmin();
        setAdminUser(null);
      },
    }),
    [adminUser]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider");
  }
  return context;
}
