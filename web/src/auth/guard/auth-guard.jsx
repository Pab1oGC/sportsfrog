import { useState, useEffect } from "react";
import { Navigate } from "react-router";
import { useAuthContext } from "src/auth/hooks";
import { SplashScreen } from "src/components/splash-screen";

export function AuthGuard({ children }) {
  const { authenticated, loading } = useAuthContext();
  const [ready, setReady] = useState(false);
  useEffect(() => { if (!loading) setReady(true); }, [loading]);
  if (!ready) return <SplashScreen />;
  if (!authenticated) return <Navigate to="/auth/jwt/sign-in" replace />;
  return children;
}
