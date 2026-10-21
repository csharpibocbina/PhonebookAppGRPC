import { useSyncExternalStore } from "react";

/** One recorded gRPC call, shown in the "gRPC inspector" drawer. */
export interface CallRecord {
  id: number;
  service: string;
  method: string;
  code: string;
  durationMs: number;
  at: Date;
}

const MAX = 50;
let calls: CallRecord[] = [];
let nextId = 1;
const listeners = new Set<() => void>();

export const inspector = {
  record(call: Omit<CallRecord, "id" | "at">) {
    calls = [{ ...call, id: nextId++, at: new Date() }, ...calls].slice(0, MAX);
    listeners.forEach((l) => l());
  },
  clear() {
    calls = [];
    listeners.forEach((l) => l());
  },
};

export function useCallLog() {
  return useSyncExternalStore(
    (cb) => {
      listeners.add(cb);
      return () => listeners.delete(cb);
    },
    () => calls,
  );
}
