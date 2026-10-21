import { useSyncExternalStore } from "react";
import type { AppNotification } from "./types";

/** Keeps the last 20 notifications of this session for the bell menu. */
const MAX = 20;
let items: AppNotification[] = [];
let unread = 0;
const listeners = new Set<() => void>();
let snapshot = { items, unread };

function emit() {
  snapshot = { items, unread };
  listeners.forEach((l) => l());
}

export const notificationStore = {
  add(n: AppNotification) {
    items = [n, ...items].slice(0, MAX);
    unread++;
    emit();
  },
  markAllRead() {
    unread = 0;
    emit();
  },
  clear() {
    items = [];
    unread = 0;
    emit();
  },
};

export function useNotificationHistory() {
  return useSyncExternalStore(
    (cb) => {
      listeners.add(cb);
      return () => listeners.delete(cb);
    },
    () => snapshot,
  );
}
