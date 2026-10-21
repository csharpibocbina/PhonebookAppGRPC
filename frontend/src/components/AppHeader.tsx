import * as Dropdown from "@radix-ui/react-dropdown-menu";
import { Bell, LogOut, Radio } from "lucide-react";
import { HubConnectionState } from "@microsoft/signalr";
import { useAuth } from "@/auth/AuthContext";
import { notificationStore, useNotificationHistory } from "@/notifications/notificationStore";
import { avatarGradient, cn } from "@/lib/utils";
import { Logo } from "./Logo";
import { ThemeToggle } from "./ThemeToggle";
import { Button } from "./ui/button";

const menuClass = "z-50 min-w-56 animate-fade-up rounded-2xl border bg-card p-1.5 shadow-xl";

export function AppHeader({ hubState, onOpenInspector }: { hubState: HubConnectionState; onOpenInspector(): void }) {
  const { user, logout } = useAuth();
  const name = user?.displayName ?? "";

  return (
    <header className="sticky top-0 z-30 border-b bg-background/80 backdrop-blur-lg">
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-2 px-4">
        <Logo to="/contacts" />
        <div className="flex-1" />
        <LiveIndicator state={hubState} />
        <Button variant="ghost" size="icon" onClick={onOpenInspector} aria-label="Open gRPC inspector" title="gRPC inspector">
          <Radio className="size-5" />
        </Button>
        <NotificationBell />
        <ThemeToggle />
        <Dropdown.Root>
          <Dropdown.Trigger asChild>
            <button
              className={cn("ml-1 grid size-9 place-items-center rounded-full bg-gradient-to-br text-sm font-semibold text-white", avatarGradient(name))}
              aria-label="Account menu"
            >
              {name.charAt(0).toUpperCase() || "?"}
            </button>
          </Dropdown.Trigger>
          <Dropdown.Portal>
            <Dropdown.Content align="end" sideOffset={8} className={menuClass}>
              <div className="px-3 py-2">
                <p className="text-sm font-semibold">{name}</p>
                <p className="truncate text-xs text-muted-foreground">{user?.email}</p>
              </div>
              <Dropdown.Separator className="my-1 h-px bg-border" />
              <Dropdown.Item
                onSelect={() => void logout()}
                className="flex cursor-pointer items-center gap-2 rounded-xl px-3 py-2 text-sm outline-none data-[highlighted]:bg-muted"
              >
                <LogOut className="size-4" /> Sign out
              </Dropdown.Item>
            </Dropdown.Content>
          </Dropdown.Portal>
        </Dropdown.Root>
      </div>
    </header>
  );
}

function LiveIndicator({ state }: { state: HubConnectionState }) {
  const live = state === HubConnectionState.Connected;
  return (
    <span
      className="mr-1 hidden items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs text-muted-foreground sm:inline-flex"
      title={live ? "Real-time notifications connected (SignalR)" : `SignalR: ${state}`}
    >
      <span className={cn("size-2 rounded-full", live ? "bg-emerald-500 shadow-[0_0_8px] shadow-emerald-500" : "bg-amber-500")} />
      {live ? "Live" : "Offline"}
    </span>
  );
}

function NotificationBell() {
  const { items, unread } = useNotificationHistory();
  return (
    <Dropdown.Root onOpenChange={(open) => open && notificationStore.markAllRead()}>
      <Dropdown.Trigger asChild>
        <Button variant="ghost" size="icon" aria-label={unread ? `Notifications, ${unread} unread` : "Notifications"} className="relative">
          <Bell className="size-5" />
          {unread > 0 && (
            <span className="absolute top-1.5 right-1.5 grid min-w-4 place-items-center rounded-full bg-rose-500 px-1 text-[10px] font-bold text-white">
              {unread}
            </span>
          )}
        </Button>
      </Dropdown.Trigger>
      <Dropdown.Portal>
        <Dropdown.Content align="end" sideOffset={8} className={cn(menuClass, "w-80")}>
          <p className="px-3 py-2 text-sm font-semibold">Notifications</p>
          {items.length === 0 ? (
            <p className="px-3 pb-3 text-sm text-muted-foreground">Nothing yet. Add or edit a contact to see a live notification.</p>
          ) : (
            <ul className="max-h-80 overflow-y-auto">
              {items.map((n) => (
                <li key={n.id} className="rounded-xl px-3 py-2 hover:bg-muted">
                  <p className="text-sm font-medium">{n.title}</p>
                  <p className="text-xs text-muted-foreground">{n.message}</p>
                  <p className="mt-0.5 text-[11px] text-muted-foreground/70">{new Date(n.createdAt).toLocaleTimeString()}</p>
                </li>
              ))}
            </ul>
          )}
        </Dropdown.Content>
      </Dropdown.Portal>
    </Dropdown.Root>
  );
}
