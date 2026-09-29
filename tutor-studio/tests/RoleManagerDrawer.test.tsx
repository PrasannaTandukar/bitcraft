import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RoleManagerDrawer from "../src/features/admin/components/RoleManagerDrawer.jsx";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------
// `initial` is a pure formatting helper; mocked for a deterministic Avatar
// label rather than depending on its real implementation.
// __esModule: true avoids TS/CJS interop issues where the mock factory's
// `default`/named exports get wrapped an extra layer (see notes from the
// ModelCatalogManager suite — this bit us there).

vi.mock("../src/features/admin/utils/formatting.js", () => ({
  __esModule: true,
  initial: vi.fn((email: string) => (email ? email[0].toUpperCase() : "?")),
}));

import { initial } from "../src/features/admin/utils/formatting.js";
const mockInitial = vi.mocked(initial);

// ---------------------------------------------------------------------------
// Fixtures / helpers
// ---------------------------------------------------------------------------

const baseUser = {
  userId: "u1",
  email: "jane@example.com",
  roles: ["Editor", "Auditor"],
};

const allRoles = ["Admin", "Editor", "Auditor"];

function setup(overrides: Record<string, any> = {}) {
  const props = {
    user: baseUser,
    allRoles,
    busy: "",
    manageError: null,
    onAssign: vi.fn().mockResolvedValue(true),
    onRevoke: vi.fn(),
    onClose: vi.fn(),
    ...overrides,
  };
  const utils = render(<RoleManagerDrawer {...props} />);
  return { ...utils, props };
}

// rc-select keeps a hidden 0x0 accessibility mirror list nested INSIDE the
// same `.ant-select-dropdown` container as the real, visible option list —
// so scoping to `.ant-select-dropdown` alone still matches both. Only the
// real, clickable option row carries a `title` attribute, so `getByTitle`
// is what actually disambiguates it (getByText matches both nodes, since
// they share the same visible label text).
async function selectRole(user: ReturnType<typeof userEvent.setup>, roleName: string) {
  await user.click(screen.getByRole("combobox"));
  const dropdown = await waitFor(() => {
    const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
    if (!el) throw new Error("Select dropdown is not open yet");
    return el as HTMLElement;
  });
  const option = within(dropdown).getByTitle(roleName);
  await user.click(option);
}

beforeEach(() => {
  vi.clearAllMocks();
  mockInitial.mockImplementation((email: string) => (email ? email[0].toUpperCase() : "?"));
});

// ---------------------------------------------------------------------------
// Visibility / basic rendering
// ---------------------------------------------------------------------------

describe("RoleManagerDrawer - visibility", () => {
  it("does not render drawer content when user is null", () => {
    setup({ user: null });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("renders the drawer when a user is provided", () => {
    setup();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(within(screen.getByRole("dialog")).getByText("Manage roles")).toBeInTheDocument();
  });

  it("calls onClose when the close button is clicked", async () => {
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(screen.getByRole("button", { name: /close/i }));
    expect(props.onClose).toHaveBeenCalledTimes(1);
  });
});

// ---------------------------------------------------------------------------
// User header
// ---------------------------------------------------------------------------

describe("RoleManagerDrawer - user header", () => {
  it("renders the user's email and workspace access label", () => {
    setup();
    expect(screen.getByText("jane@example.com")).toBeInTheDocument();
    expect(screen.getByText("Workspace access")).toBeInTheDocument();
  });

  it("renders the avatar using the initial() helper on the user's email", () => {
    mockInitial.mockReturnValue("J");
    setup();
    expect(screen.getByText("J")).toBeInTheDocument();
    expect(mockInitial).toHaveBeenCalledWith("jane@example.com");
  });

  it("shows the manageError alert when present", () => {
    setup({ manageError: "Could not update roles for this user." });
    expect(screen.getByText("Could not update roles for this user.")).toBeInTheDocument();
  });

  it("does not show an alert when manageError is null", () => {
    setup({ manageError: null });
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Assign role
// ---------------------------------------------------------------------------

describe("RoleManagerDrawer - assign role", () => {
  it("only offers roles the user does not already have", async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole("combobox"));
    const dropdown = await waitFor(() => {
      const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
      if (!el) throw new Error("dropdown not open");
      return el as HTMLElement;
    });
    expect(within(dropdown).getByTitle("Admin")).toBeInTheDocument();
    expect(within(dropdown).queryByTitle("Editor")).not.toBeInTheDocument();
    expect(within(dropdown).queryByTitle("Auditor")).not.toBeInTheDocument();
  });

  it("shows a 'Select a role' placeholder when roles are available", () => {
    setup();
    expect(screen.getByText("Select a role")).toBeInTheDocument();
  });

  it("shows an 'All roles assigned' placeholder and disables the select when no roles remain", () => {
    setup({ user: { ...baseUser, roles: allRoles } });
    expect(screen.getByText("All roles assigned")).toBeInTheDocument();
    expect(screen.getByRole("combobox")).toBeDisabled();
  });

  it("disables the select while a request is in flight (busy is truthy)", () => {
    setup({ busy: "add:Admin" });
    expect(screen.getByRole("combobox")).toBeDisabled();
  });

  it("disables the Assign button until a role is selected", () => {
    setup();
    expect(screen.getByRole("button", { name: /assign/i })).toBeDisabled();
  });

  it("enables the Assign button once a role is selected", async () => {
    const user = userEvent.setup();
    setup();
    await selectRole(user, "Admin");
    expect(screen.getByRole("button", { name: /assign/i })).toBeEnabled();
  });

  it("calls onAssign with the user id and selected role", async () => {
    const user = userEvent.setup();
    const { props } = setup();
    await selectRole(user, "Admin");
    await user.click(screen.getByRole("button", { name: /assign/i }));
    expect(props.onAssign).toHaveBeenCalledWith("u1", "Admin");
  });

  it("clears the selected role after a successful assignment", async () => {
    const user = userEvent.setup();
    const onAssign = vi.fn().mockResolvedValue(true);
    setup({ onAssign });
    await selectRole(user, "Admin");
    await user.click(screen.getByRole("button", { name: /assign/i }));
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /assign/i })).toBeDisabled();
    });
    expect(screen.getByText("Select a role")).toBeInTheDocument();
  });

  it("keeps the selected role if the assignment fails", async () => {
    const user = userEvent.setup();
    const onAssign = vi.fn().mockResolvedValue(false);
    setup({ onAssign });
    await selectRole(user, "Admin");
    await user.click(screen.getByRole("button", { name: /assign/i }));
    await waitFor(() => expect(onAssign).toHaveBeenCalled());
    expect(screen.getByRole("button", { name: /assign/i })).toBeEnabled();
  });

  it("shows a loading indicator on Assign while busy starts with 'add:'", () => {
    setup({ busy: "add:Admin" });
    const assignButton = screen.getByRole("button", { name: /assign/i });
    expect(assignButton.querySelector(".anticon-loading")).toBeTruthy();
  });

  it("does not show a loading indicator on Assign for an unrelated busy value", () => {
    setup({ busy: "revoke:Editor" });
    const assignButton = screen.getByRole("button", { name: /assign/i });
    expect(assignButton.querySelector(".anticon-loading")).toBeFalsy();
  });
});

// ---------------------------------------------------------------------------
// Assigned roles / revoke
// ---------------------------------------------------------------------------

describe("RoleManagerDrawer - assigned roles", () => {
  it("renders a tag and a Revoke button for each assigned role", () => {
    setup();
    expect(screen.getByText("Editor")).toBeInTheDocument();
    expect(screen.getByText("Auditor")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /revoke/i })).toHaveLength(2);
  });

  it("shows an empty state when the user has no roles", () => {
    setup({ user: { ...baseUser, roles: [] } });
    expect(screen.getByText("No roles assigned")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /revoke/i })).not.toBeInTheDocument();
  });

  it("calls onRevoke with the user id and role after confirming", async () => {
    const user = userEvent.setup();
    const { props } = setup();
    const revokeButtons = screen.getAllByRole("button", { name: /revoke/i });
    // baseUser.roles === ["Editor", "Auditor"], so index 0 is Editor's button.
    await user.click(revokeButtons[0]);

    const confirmButton = await screen.findByRole("button", { name: "Revoke role" });
    await user.click(confirmButton);

    expect(props.onRevoke).toHaveBeenCalledWith("u1", "Editor");
  });

  it("does not call onRevoke if the confirmation is cancelled", async () => {
    const user = userEvent.setup();
    const { props } = setup();
    const revokeButtons = screen.getAllByRole("button", { name: /revoke/i });
    await user.click(revokeButtons[0]);

    // Popconfirm's default cancel button is labelled "Cancel".
    const cancelButton = await screen.findByRole("button", { name: /^cancel$/i });
    await user.click(cancelButton);

    expect(props.onRevoke).not.toHaveBeenCalled();
  });

  it("shows a loading indicator only on the Revoke button matching busy", () => {
    setup({ busy: "revoke:Auditor" });
    const revokeButtons = screen.getAllByRole("button", { name: /revoke/i });
    // baseUser.roles === ["Editor", "Auditor"]
    expect(revokeButtons[0].querySelector(".anticon-loading")).toBeFalsy();
    expect(revokeButtons[1].querySelector(".anticon-loading")).toBeTruthy();
  });
});
