import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Code, ConnectError } from "@connectrpc/connect";
import { Route, Routes } from "react-router";
import { renderWithProviders } from "@/test/utils";
import { LoginPage, DEMO } from "./LoginPage";

const login = vi.fn();
// A plain function (not vi.fn) for the rejecting case: Vitest reports a vi.fn that
// returns a rejected promise as an unhandled error even when the page catches it.
let loginOverride: ((email: string, password: string) => Promise<void>) | null = null;
vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ login: (e: string, p: string) => (loginOverride ? loginOverride(e, p) : login(e, p)) }),
}));

function renderLogin() {
  return renderWithProviders(
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/contacts" element={<p>Contacts page</p>} />
    </Routes>,
    { route: "/login" },
  );
}

describe("LoginPage", () => {
  beforeEach(() => {
    login.mockReset();
    loginOverride = null;
  });

  it("validates the form before calling the server", async () => {
    renderLogin();

    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it("fills in the demo account and signs in", async () => {
    login.mockResolvedValue(undefined);
    renderLogin();

    await userEvent.click(screen.getByRole("button", { name: /demo account/i }));
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(login).toHaveBeenCalledWith(DEMO.email, DEMO.password));
    expect(await screen.findByText("Contacts page")).toBeInTheDocument();
  });

  it("shows the server's message on UNAUTHENTICATED", async () => {
    let rejected = false;
    loginOverride = async () => {
      rejected = true;
      throw new ConnectError("Invalid email or password.", Code.Unauthenticated);
    };
    renderLogin();

    await userEvent.type(screen.getByLabelText("Email"), "jay@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "wrong");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(rejected).toBe(true));
    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid email or password.");
  });
});
