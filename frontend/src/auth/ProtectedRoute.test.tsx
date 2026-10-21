import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router";
import { renderWithProviders } from "@/test/utils";
import { ProtectedRoute } from "./ProtectedRoute";

const auth = { status: "anonymous" as string };
vi.mock("./AuthContext", () => ({ useAuth: () => auth }));

function renderAt(status: string) {
  auth.status = status;
  return renderWithProviders(
    <Routes>
      <Route element={<ProtectedRoute />}>
        <Route path="/contacts" element={<p>Secret contacts</p>} />
      </Route>
      <Route path="/login" element={<p>Login page</p>} />
    </Routes>,
    { route: "/contacts" },
  );
}

describe("ProtectedRoute", () => {
  it("redirects signed-out users to /login", () => {
    renderAt("anonymous");
    expect(screen.getByText("Login page")).toBeInTheDocument();
  });

  it("shows a spinner while the session is being restored", () => {
    renderAt("loading");
    expect(screen.getByRole("status", { name: "Loading" })).toBeInTheDocument();
  });

  it("renders the page for signed-in users", () => {
    renderAt("authenticated");
    expect(screen.getByText("Secret contacts")).toBeInTheDocument();
  });
});
