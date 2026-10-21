import { useEffect, useState } from "react";
import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { tokenStore } from "@/api/tokenStore";
import { showNotification } from "./notify";
import { notificationStore } from "./notificationStore";
import type { AppNotification } from "./types";

export const HUB_URL = import.meta.env.VITE_HUB_URL ?? "/hubs/notifications";

/**
 * Opens a SignalR connection while the user is signed in.
 *
 * gRPC is request/response: the browser asks, the server answers. SignalR is
 * the other direction: the server pushes "Notify" events whenever something
 * happens (a contact changed in another tab, an email was sent, ...).
 */
export function useNotificationHub(enabled: boolean) {
  const queryClient = useQueryClient();
  const [state, setState] = useState<HubConnectionState>(HubConnectionState.Disconnected);

  useEffect(() => {
    if (!enabled) return;

    const connection = new HubConnectionBuilder()
      // WebSockets can't send an Authorization header, so SignalR passes this
      // token as ?access_token=... It's read again on every reconnect.
      .withUrl(HUB_URL, { accessTokenFactory: () => tokenStore.get() ?? "" })
      .withAutomaticReconnect()
      // We report failures ourselves; this hides the noisy "stopped during negotiation"
      // message React StrictMode causes in dev by mounting effects twice.
      .configureLogging(LogLevel.Critical)
      .build();

    connection.on("Notify", (n: AppNotification) => {
      notificationStore.add(n);
      showNotification(n);
      // Keep every open tab in sync: refetch the contact list after changes.
      if (n.type.startsWith("Contact")) queryClient.invalidateQueries({ queryKey: ["contacts"] });
    });

    const update = () => setState(connection.state);
    connection.onreconnecting(update);
    connection.onreconnected(update);
    connection.onclose(update);

    let stopped = false;
    connection
      .start()
      .then(update)
      .catch((err) => {
        if (!stopped) console.warn("SignalR connection failed", err);
        update();
      });

    return () => {
      stopped = true;
      void connection.stop();
    };
  }, [enabled, queryClient]);

  return state;
}
