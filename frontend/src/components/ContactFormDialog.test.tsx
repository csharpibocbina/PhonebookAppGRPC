import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Code, ConnectError } from "@connectrpc/connect";
import { renderWithProviders } from "@/test/utils";
import { phonebookClient } from "@/api/clients";
import { ContactFormDialog, contactSchema } from "./ContactFormDialog";

vi.mock("@/api/clients", () => ({ phonebookClient: { createContact: vi.fn(), updateContact: vi.fn() } }));

describe("contactSchema", () => {
  const valid = { firstName: "Ana", lastName: "", phoneNumber: "+63 917 555 0101", email: "", address: "", notes: "", isFavorite: false };

  it.each(["+63 917 555 0101", "(02) 8555-0101", "09175550101"])("accepts %s", (phoneNumber) => {
    expect(contactSchema.safeParse({ ...valid, phoneNumber }).success).toBe(true);
  });

  it.each(["abc", "12345", "1234567890123456"])("rejects %s", (phoneNumber) => {
    expect(contactSchema.safeParse({ ...valid, phoneNumber }).success).toBe(false);
  });
});

describe("ContactFormDialog", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows validation errors without calling the server", async () => {
    renderWithProviders(<ContactFormDialog open onOpenChange={() => {}} />);

    await userEvent.click(screen.getByRole("button", { name: "Add contact" }));

    expect(await screen.findByText("First name is required.")).toBeInTheDocument();
    expect(screen.getByText("Phone number is required.")).toBeInTheDocument();
    expect(phonebookClient.createContact).not.toHaveBeenCalled();
  });

  it("creates a contact and closes", async () => {
    vi.mocked(phonebookClient.createContact).mockResolvedValue({} as never);
    const onOpenChange = vi.fn();
    renderWithProviders(<ContactFormDialog open onOpenChange={onOpenChange} />);

    await userEvent.type(screen.getByLabelText("First name *"), "Ana");
    await userEvent.type(screen.getByLabelText("Phone number *"), "+63 919 555 0103");
    await userEvent.click(screen.getByRole("button", { name: "Add contact" }));

    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
    expect(phonebookClient.createContact).toHaveBeenCalledWith(expect.objectContaining({ firstName: "Ana", phoneNumber: "+63 919 555 0103" }));
  });

  it("shows server-side field errors from INVALID_ARGUMENT", async () => {
    vi.mocked(phonebookClient.createContact).mockRejectedValue(
      new ConnectError("invalid", Code.InvalidArgument, { "validation-errors": JSON.stringify({ email: ["Server says no."] }) }),
    );
    renderWithProviders(<ContactFormDialog open onOpenChange={() => {}} />);

    await userEvent.type(screen.getByLabelText("First name *"), "Ana");
    await userEvent.type(screen.getByLabelText("Phone number *"), "+63 919 555 0103");
    await userEvent.click(screen.getByRole("button", { name: "Add contact" }));

    expect(await screen.findByText("Server says no.")).toBeInTheDocument();
  });
});
