import { toast } from "sonner";
import { NotificationToast, TOAST_DURATION_MS } from "./NotificationToast";
import type { AppNotification } from "./types";

/** Shows a notification toast that hides itself after 5 seconds. */
export function showNotification(notification: Pick<AppNotification, "title" | "message" | "level"> & { id?: string }) {
  return toast.custom((id) => <NotificationToast notification={notification} onClose={() => toast.dismiss(id)} />, {
    id: notification.id,
    duration: TOAST_DURATION_MS,
  });
}

export function showError(message: string) {
  return showNotification({ title: "Something went wrong", message, level: "error" });
}
