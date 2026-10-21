import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { authClient } from "@/api/clients";
import { refreshSession } from "@/api/transport";
import { tokenStore } from "@/api/tokenStore";
import type { AuthResponse, UserProfile } from "@/gen/auth/v1/auth_pb";

type Status = "loading" | "authenticated" | "anonymous";

interface AuthContextValue {
  status: Status;
  user: UserProfile | null;
  login(email: string, password: string): Promise<void>;
  register(email: string, password: string, displayName: string): Promise<void>;
  logout(): Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/** Refresh this long before the access token expires. */
const REFRESH_MARGIN_MS = 60_000;

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<Status>("loading");
  const [user, setUser] = useState<UserProfile | null>(null);

  const accept = useCallback((res: AuthResponse) => {
    tokenStore.set(res.accessToken, res.accessTokenExpiresAt);
    setUser(res.user ?? null);
    setStatus("authenticated");
  }, []);

  const signOutLocally = useCallback(() => {
    tokenStore.clear();
    setUser(null);
    setStatus("anonymous");
    queryClient.clear();
  }, [queryClient]);

  // On page load there's no access token in memory. If the browser still has
  // the httpOnly refresh cookie, Refresh gives us a new one silently.
  useEffect(() => {
    let cancelled = false;
    refreshSession()
      .then((res) => !cancelled && accept(res))
      .catch(() => !cancelled && setStatus("anonymous"));
    return () => {
      cancelled = true;
    };
  }, [accept]);

  // If a background refresh fails (session revoked/expired), drop to signed out.
  useEffect(
    () =>
      tokenStore.subscribe(() => {
        if (!tokenStore.get() && status === "authenticated") {
          setUser(null);
          setStatus("anonymous");
        }
      }),
    [status],
  );

  // Proactively refresh shortly before the access token expires.
  useEffect(() => {
    if (status !== "authenticated") return;
    const schedule = () => Math.max(tokenStore.expiresAt() - Date.now() - REFRESH_MARGIN_MS, 5_000);
    let timer = window.setTimeout(function tick() {
      refreshSession()
        .then(() => (timer = window.setTimeout(tick, schedule())))
        .catch(() => signOutLocally());
    }, schedule());
    return () => window.clearTimeout(timer);
  }, [status, signOutLocally]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      async login(email, password) {
        accept(await authClient.login({ email, password }));
      },
      async register(email, password, displayName) {
        accept(await authClient.register({ email, password, displayName }));
      },
      async logout() {
        try {
          await authClient.logout({});
        } finally {
          signOutLocally();
        }
      },
    }),
    [status, user, accept, signOutLocally],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}
