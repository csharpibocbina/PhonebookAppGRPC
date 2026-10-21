import { Code, ConnectError } from "@connectrpc/connect";

/** Field errors the server puts in the "validation-errors" trailer (INVALID_ARGUMENT). */
export type FieldErrors = Record<string, string[]>;

export function getFieldErrors(err: unknown): FieldErrors {
  const error = ConnectError.from(err);
  if (error.code !== Code.InvalidArgument) return {};
  const raw = error.metadata.get("validation-errors");
  if (!raw) return {};
  try {
    return JSON.parse(raw) as FieldErrors;
  } catch {
    return {};
  }
}

/** A short, human message for a failed gRPC call. */
export function errorMessage(err: unknown): string {
  const error = ConnectError.from(err);
  switch (error.code) {
    case Code.Unavailable:
      return "Can't reach the server. Is the API running?";
    case Code.ResourceExhausted:
      return "Too many attempts. Please wait a minute and try again.";
    case Code.Internal:
    case Code.Unknown:
      return "Something went wrong. Please try again.";
    default:
      return error.rawMessage || "Something went wrong.";
  }
}
