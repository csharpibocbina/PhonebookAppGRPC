import { render, screen } from "@testing-library/react";
import { toast } from "sonner";
import { NotificationToast, TOAST_DURATION_MS } from "./NotificationToast";
import { showNotification } from "./notify";

vi.mock("sonner", () => ({ toast: { custom: vi.fn(), dismiss: vi.fn() } }));

describe("notification toasts", () => {
  it("are shown for exactly 5 seconds", () => {
    showNotification({ id: "n1", title: "Contact added", message: "Maria was added.", level: "success" });

    expect(TOAST_DURATION_MS).toBe(5000);
    expect(toast.custom).toHaveBeenCalledWith(expect.any(Function), { id: "n1", duration: 5000 });
  });

  it("render the title, message and a 5s countdown bar", () => {
    render(<NotificationToast notification={{ title: "Contact added", message: "Maria was added.", level: "success" }} />);

    expect(screen.getByText("Contact added")).toBeInTheDocument();
    expect(screen.getByText("Maria was added.")).toBeInTheDocument();
    expect(screen.getByTestId("toast-progress").style.getPropertyValue("--toast-duration")).toBe("5000ms");
  });

  it("can be dismissed early", async () => {
    const onClose = vi.fn();
    render(<NotificationToast notification={{ title: "Hi", message: "There", level: "info" }} onClose={onClose} />);

    screen.getByRole("button", { name: /dismiss/i }).click();

    expect(onClose).toHaveBeenCalled();
  });
});
