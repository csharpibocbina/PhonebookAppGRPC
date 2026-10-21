import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { useNotificationHub } from "./useNotificationHub";
import { showNotification } from "./notify";

// A fake SignalR connection that lets the test "push" events.
const handlers = new Map<string, (arg: unknown) => void>();
const connection = {
  state: "Connected",
  on: vi.fn((name: string, cb: (arg: unknown) => void) => handlers.set(name, cb)),
  onreconnecting: vi.fn(),
  onreconnected: vi.fn(),
  onclose: vi.fn(),
  start: vi.fn(() => Promise.resolve()),
  stop: vi.fn(() => Promise.resolve()),
};

vi.mock("@microsoft/signalr", () => ({
  HubConnectionState: { Connected: "Connected", Disconnected: "Disconnected" },
  LogLevel: { Critical: 5 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return connection; }
  },
}));
vi.mock("./notify", () => ({ showNotification: vi.fn() }));

function setup(enabled: boolean) {
  const queryClient = new QueryClient();
  const invalidate = vi.spyOn(queryClient, "invalidateQueries");
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  const hook = renderHook(() => useNotificationHub(enabled), { wrapper });
  return { ...hook, invalidate };
}

describe("useNotificationHub", () => {
  beforeEach(() => {
    handlers.clear();
    vi.clearAllMocks();
  });

  it("does not connect while signed out", () => {
    setup(false);
    expect(connection.start).not.toHaveBeenCalled();
  });

  it("shows a toast and refreshes contacts when the server pushes a contact event", async () => {
    const { invalidate } = setup(true);
    await waitFor(() => expect(connection.start).toHaveBeenCalled());

    const event = { id: "1", type: "ContactCreated", title: "Contact added", message: "Ana was added.", level: "success", createdAt: "" };
    handlers.get("Notify")!(event);

    expect(showNotification).toHaveBeenCalledWith(event);
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["contacts"] });
  });

  it("does not refetch contacts for other events", async () => {
    const { invalidate } = setup(true);
    await waitFor(() => expect(connection.start).toHaveBeenCalled());

    handlers.get("Notify")!({ id: "2", type: "EmailSent", title: "Email sent", message: "", level: "success", createdAt: "" });

    expect(showNotification).toHaveBeenCalled();
    expect(invalidate).not.toHaveBeenCalled();
  });

  it("stops the connection on unmount", async () => {
    const { unmount } = setup(true);
    await waitFor(() => expect(connection.start).toHaveBeenCalled());
    unmount();
    expect(connection.stop).toHaveBeenCalled();
  });
});
