import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";

import UsersTable from "../src/features/admin/components/UsersTable";

const users = [
  {
    userId: "1",
    email: "alice@example.com",
    roles: ["admin", "editor"],
  },
  {
    userId: "2",
    email: "bob@example.com",
    roles: ["user"],
  },
  {
    userId: "3",
    email: "charlie@example.com",
    roles: [],
  },
];

describe("UsersTable", () => {
  it("renders users and their roles", () => {
    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={vi.fn()}
      />
    );

    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
    expect(screen.getByText("bob@example.com")).toBeInTheDocument();
    expect(screen.getByText("charlie@example.com")).toBeInTheDocument();

    expect(screen.getByText("admin")).toBeInTheDocument();
    expect(screen.getByText("editor")).toBeInTheDocument();
    expect(screen.getByText("user")).toBeInTheDocument();
  });

  it("shows 'No roles assigned' for a user with no roles", () => {
    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={vi.fn()}
      />
    );

    expect(screen.getByText("No roles assigned")).toBeInTheDocument();
  });

  it("filters users by email", () => {
    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={vi.fn()}
      />
    );

    const searchInput = screen.getByPlaceholderText("Search by email or role");

    fireEvent.change(searchInput, {
      target: { value: "alice@example.com" },
    });

    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
    expect(screen.queryByText("bob@example.com")).not.toBeInTheDocument();
    expect(screen.queryByText("charlie@example.com")).not.toBeInTheDocument();
  });

  it("filters users by role", () => {
    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={vi.fn()}
      />
    );

    const searchInput = screen.getByPlaceholderText("Search by email or role");

    fireEvent.change(searchInput, {
      target: { value: "editor" },
    });

    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
    expect(screen.queryByText("bob@example.com")).not.toBeInTheDocument();
    expect(screen.queryByText("charlie@example.com")).not.toBeInTheDocument();
  });

  it("is case-insensitive when filtering", () => {
    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={vi.fn()}
      />
    );

    const searchInput = screen.getByPlaceholderText("Search by email or role");

    fireEvent.change(searchInput, {
      target: { value: "ALICE" },
    });

    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
    expect(screen.queryByText("bob@example.com")).not.toBeInTheDocument();
  });

  it("shows the empty state when no users match the search", () => {
    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={vi.fn()}
      />
    );

    const searchInput = screen.getByPlaceholderText("Search by email or role");

    fireEvent.change(searchInput, {
      target: { value: "does-not-exist" },
    });

    expect(screen.getByText("No users found")).toBeInTheDocument();
    expect(screen.queryByText("alice@example.com")).not.toBeInTheDocument();
    expect(screen.queryByText("bob@example.com")).not.toBeInTheDocument();
    expect(screen.queryByText("charlie@example.com")).not.toBeInTheDocument();
  });

  it("calls onManageRoles with the selected user", () => {
    const onManageRoles = vi.fn();

    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={onManageRoles}
      />
    );

    const manageButtons = screen.getAllByRole("button", {
      name: /manage roles/i,
    });

    fireEvent.click(manageButtons[0]);

    expect(onManageRoles).toHaveBeenCalledTimes(1);
    expect(onManageRoles).toHaveBeenCalledWith(users[0]);
  });

  it("clears the search and shows all users again", () => {
    render(
      <UsersTable
        users={users}
        loading={false}
        onManageRoles={vi.fn()}
      />
    );

    const searchInput = screen.getByPlaceholderText("Search by email or role");

    fireEvent.change(searchInput, {
      target: { value: "alice" },
    });

    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
    expect(screen.queryByText("bob@example.com")).not.toBeInTheDocument();

    fireEvent.change(searchInput, {
      target: { value: "" },
    });

    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
    expect(screen.getByText("bob@example.com")).toBeInTheDocument();
    expect(screen.getByText("charlie@example.com")).toBeInTheDocument();
  });

  it("shows the loading state", () => {
    const { container } = render(
    <UsersTable
      users={users}
      loading={true}
      onManageRoles={vi.fn()}
    />
  );

    expect(container.querySelector(".ant-spin-spinning")).toBeInTheDocument();
  });
});
