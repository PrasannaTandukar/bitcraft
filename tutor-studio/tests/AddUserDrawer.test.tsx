import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import AddUserDrawer from "../src/features/admin/components/AddUserDrawer";

function setup(overrides = {}) {
  const props = {
    open: true,
    saving: false,
    registerError: null,
    onSubmit: vi.fn().mockResolvedValue(true),
    onClose: vi.fn(),
    ...overrides,
  };
  render(<AddUserDrawer {...props} />);
  return props;
}

describe("AddUserDrawer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the email and password fields when open", () => {
    setup();
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /add user/i })).toBeInTheDocument();
  });

  it("shows validation errors when submitting an empty form", async () => {
    const user = userEvent.setup();
    const { onSubmit } = setup();
    await user.click(screen.getByRole("button", { name: /add user/i }));
    expect(await screen.findByText("Enter an email address.")).toBeInTheDocument();
    expect(await screen.findByText("Enter a password.")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("rejects an invalid email address", async () => {
    const user = userEvent.setup();
    const { onSubmit } = setup();
    await user.type(screen.getByLabelText(/email/i), "not-an-email");
    await user.click(screen.getByRole("button", { name: /add user/i }));
    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("rejects a password that does not meet the complexity rules", async () => {
    const user = userEvent.setup();
    const { onSubmit } = setup();
    await user.type(screen.getByLabelText(/email/i), "user@bitcraft.com");
    await user.type(screen.getByLabelText(/password/i), "weakpassword");
    await user.click(screen.getByRole("button", { name: /add user/i }));
    expect(
      await screen.findByText(
        "Password needs an uppercase letter, lowercase letter, digit, and symbol."
      )
    ).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("submits valid values and closes the drawer on success", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(true);
    const onClose = vi.fn();
    setup({ onSubmit, onClose });
    await user.type(screen.getByLabelText(/email/i), "user@bitcraft.com");
    await user.type(screen.getByLabelText(/password/i), "Admin1234!");
    await user.click(screen.getByRole("button", { name: /add user/i }));
    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith({
        email: "user@bitcraft.com",
        password: "Admin1234!",
      })
    );
    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
  });

  it("keeps the drawer open when submission fails", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(false);
    const onClose = vi.fn();
    setup({ onSubmit, onClose });
    await user.type(screen.getByLabelText(/email/i), "user@bitcraft.com");
    await user.type(screen.getByLabelText(/password/i), "Admin1234!");
    await user.click(screen.getByRole("button", { name: /add user/i }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onClose).not.toHaveBeenCalled();
  });

  it("calls onClose when Cancel is clicked and no save is in progress", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    setup({ onClose });
    await user.click(screen.getByRole("button", { name: /cancel/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("disables Cancel while a save is in progress", () => {
    setup({ saving: true });
    expect(screen.getByRole("button", { name: /cancel/i })).toBeDisabled();
  });

  it("shows the registration error message", () => {
    setup({ registerError: "Email already exists." });
    expect(screen.getByText("Email already exists.")).toBeInTheDocument();
  });
});
