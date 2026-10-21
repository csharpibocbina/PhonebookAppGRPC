/**
 * Holds the access token in memory only (never localStorage), so a script
 * injected into the page can't read it from storage. On reload it's gone,
 * and the app gets a new one with the httpOnly refresh cookie.
 */
type Listener = () => void;

let accessToken: string | null = null;
let expiresAt = 0; // unix ms
const listeners = new Set<Listener>();

export const tokenStore = {
  get: () => accessToken,
  expiresAt: () => expiresAt,
  set(token: string, expiresAtUnixSeconds: bigint | number) {
    accessToken = token;
    expiresAt = Number(expiresAtUnixSeconds) * 1000;
    listeners.forEach((l) => l());
  },
  clear() {
    accessToken = null;
    expiresAt = 0;
    listeners.forEach((l) => l());
  },
  subscribe(listener: Listener) {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
