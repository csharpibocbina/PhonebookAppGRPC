import { CheckCircle2, Info, TriangleAlert, X, XCircle } from "lucide-react";
import { cn } from "@/lib/utils";
import type { AppNotification, NotificationLevel } from "./types";

export const TOAST_DURATION_MS = 5000;

const styles: Record<NotificationLevel, { icon: typeof Info; accent: string; bar: string }> = {
  success: { icon: CheckCircle2, accent: "text-emerald-500", bar: "bg-emerald-500" },
  info: { icon: Info, accent: "text-sky-500", bar: "bg-sky-500" },
  warning: { icon: TriangleAlert, accent: "text-amber-500", bar: "bg-amber-500" },
  error: { icon: XCircle, accent: "text-rose-500", bar: "bg-rose-500" },
};

interface Props {
  notification: Pick<AppNotification, "title" | "message" | "level">;
  onClose?: () => void;
  durationMs?: number;
}

/**
 * The card shown for each SignalR notification. It stays for 5 seconds
 * (the bar at the bottom counts down) and then hides itself. Hovering
 * pauses it.
 */
export function NotificationToast({ notification, onClose, durationMs = TOAST_DURATION_MS }: Props) {
  const { icon: Icon, accent, bar } = styles[notification.level] ?? styles.info;
  return (
    <div
      role="status"
      className="toast-card relative w-[min(92vw,360px)] overflow-hidden rounded-2xl border bg-card/95 p-4 pr-10 shadow-xl shadow-black/10 backdrop-blur"
    >
      <div className="flex gap-3">
        <Icon className={cn("mt-0.5 size-5 shrink-0", accent)} aria-hidden />
        <div className="min-w-0">
          <p className="text-sm font-semibold">{notification.title}</p>
          <p className="mt-0.5 text-sm text-muted-foreground">{notification.message}</p>
        </div>
      </div>
      {onClose && (
        <button
          type="button"
          onClick={onClose}
          aria-label="Dismiss notification"
          className="absolute top-3 right-3 rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
        >
          <X className="size-4" />
        </button>
      )}
      <div className="absolute inset-x-0 bottom-0 h-1 bg-muted">
        <div
          data-testid="toast-progress"
          className={cn("toast-progress h-full", bar)}
          style={{ ["--toast-duration" as string]: `${durationMs}ms` }}
        />
      </div>
    </div>
  );
}
