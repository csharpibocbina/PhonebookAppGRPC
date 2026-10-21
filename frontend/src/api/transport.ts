import { Code, ConnectError, createClient, type Interceptor, type Transport } from "@connectrpc/connect";
import { createGrpcWebTransport } from "@connectrpc/connect-web";
import { AuthService, type AuthResponse } from "@/gen/auth/v1/auth_pb";
import { inspector } from "./inspector";
import { tokenStore } from "./tokenStore";

/**
 * How the browser talks gRPC
 * --------------------------
 * Browsers can't speak raw gRPC (they can't control HTTP/2 frames or read
 * trailers), so we use gRPC-Web: the same protobuf messages, sent with a
 * normal fetch() POST to /api/<package>.<Service>/<Method>. ASP.NET Core
 * understands gRPC-Web natively (app.UseGrpcWeb()), so no proxy is needed.
 *
 * Interceptors are the client-side twin of server interceptors: they wrap
 * every call. We use them to attach the JWT and to record calls for the
 * gRPC inspector.
 */
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "/api";

const fetchWithCookies: typeof fetch = (input, init) => fetch(input, { ...init, credentials: "include" });

/** Records every call (service, method, status code, duration) for the inspector drawer. */
const inspectorInterceptor: Interceptor = (next) => async (req) => {
  const started = performance.now();
  const record = (code: string) =>
    inspector.record({
      service: req.service.typeName,
      method: req.method.name,
      code,
      durationMs: Math.round(performance.now() - started),
    });
  try {
    const res = await next(req);
    record("OK");
    return res;
  } catch (err) {
    record(Code[ConnectError.from(err).code]);
    throw err;
  }
};

/** Adds "authorization: Bearer <token>" metadata (= an HTTP header) to every call. */
const bearerInterceptor: Interceptor = (next) => async (req) => {
  const token = tokenStore.get();
  if (token) req.header.set("authorization", `Bearer ${token}`);
  return next(req);
};

// A transport without the retry logic, used for the refresh call itself.
const baseTransport = createGrpcWebTransport({
  baseUrl: API_BASE_URL,
  fetch: fetchWithCookies,
  interceptors: [inspectorInterceptor],
});
const refreshClient = createClient(AuthService, baseTransport);

let refreshInFlight: Promise<AuthResponse> | null = null;

/**
 * Gets a new access token using the httpOnly refresh cookie. Several calls
 * failing at once share one refresh request instead of racing each other
 * (which would trip the server's refresh-token reuse detection).
 */
export function refreshSession(): Promise<AuthResponse> {
  refreshInFlight ??= refreshClient
    .refresh({})
    .then((res) => {
      tokenStore.set(res.accessToken, res.accessTokenExpiresAt);
      return res;
    })
    .catch((err) => {
      tokenStore.clear();
      throw err;
    })
    .finally(() => {
      refreshInFlight = null;
    });
  return refreshInFlight;
}

const NO_RETRY_METHODS = new Set(["Login", "Register", "Refresh", "Logout"]);

/** On UNAUTHENTICATED: refresh the access token once, then retry the call. */
const refreshInterceptor: Interceptor = (next) => async (req) => {
  try {
    return await next(req);
  } catch (err) {
    const error = ConnectError.from(err);
    if (error.code !== Code.Unauthenticated || NO_RETRY_METHODS.has(req.method.name) || !tokenStore.get()) throw err;

    await refreshSession();
    req.header.set("authorization", `Bearer ${tokenStore.get()}`);
    return next(req);
  }
};

export const transport: Transport = createGrpcWebTransport({
  baseUrl: API_BASE_URL,
  fetch: fetchWithCookies,
  // Order matters: the first interceptor is the outermost.
  interceptors: [refreshInterceptor, inspectorInterceptor, bearerInterceptor],
});
