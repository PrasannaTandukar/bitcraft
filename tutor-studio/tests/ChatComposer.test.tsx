import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";

import ChatComposer from "../src/features/user/components/ChatComposer";

function renderComposer(
  overrides: Partial<React.ComponentProps<typeof ChatComposer>> = {},
) {
  const props = {
    value: "",
    tutorName: "Test Tutor",
    disabled: false,
    onChange: vi.fn(),
    onSend: vi.fn(),
    ...overrides,
  };

  return {
    ...render(<ChatComposer {...props} />),
    props,
  };
}

function renderControlledComposer(
  overrides: Partial<React.ComponentProps<typeof ChatComposer>> = {},
) {
  const onChange = overrides.onChange ?? vi.fn();
  const onSend = overrides.onSend ?? vi.fn();

  function ControlledComposer() {
    const [value, setValue] = useState(overrides.value ?? "");

    return (
      <ChatComposer
        value={value}
        tutorName={overrides.tutorName ?? "Test Tutor"}
        disabled={overrides.disabled ?? false}
        onChange={(nextValue) => {
          setValue(nextValue);
          onChange(nextValue);
        }}
        onSend={onSend}
      />
    );
  }

  return {
    ...render(<ControlledComposer />),
    onChange,
    onSend,
  };
}

describe("ChatComposer", () => {
  it("renders the message input and send button", () => {
    renderComposer();

    expect(screen.getByRole("textbox")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Send message" }),
    ).toBeInTheDocument();
  });

  it("shows the tutor name in the placeholder", () => {
    renderComposer({
      tutorName: "Physics Tutor",
    });

    expect(
      screen.getByPlaceholderText("Ask Physics Tutor…"),
    ).toBeInTheDocument();
  });

  it("shows the default placeholder when no tutor is selected", () => {
    renderComposer({
      tutorName: "",
    });

    expect(
      screen.getByPlaceholderText("Select a tutor to begin…"),
    ).toBeInTheDocument();
  });

  it("calls onChange with the updated message", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    renderControlledComposer({
      onChange,
    });

    const input = screen.getByRole("textbox");

    await user.type(input, "Hello tutor");

    expect(onChange).toHaveBeenLastCalledWith("Hello tutor");
    expect(onChange).toHaveBeenCalledTimes(11);
  });

  it("calls onSend when the send button is clicked", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();

    renderComposer({
      value: "Hello tutor",
      onSend,
    });

    const sendButton = screen.getByRole("button", {
      name: "Send message",
    });

    await user.click(sendButton);

    expect(onSend).toHaveBeenCalledTimes(1);
  });

  it("calls onSend when Enter is pressed", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();

    renderComposer({
      value: "Hello tutor",
      onSend,
    });

    const input = screen.getByRole("textbox");

    await user.click(input);
    await user.keyboard("{Enter}");

    expect(onSend).toHaveBeenCalledTimes(1);
  });

  it("does not send when Shift+Enter is pressed", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();

    renderComposer({
      value: "Hello tutor",
      onSend,
    });

    const input = screen.getByRole("textbox");

    await user.click(input);
    await user.keyboard("{Shift>}{Enter}{/Shift}");

    expect(onSend).not.toHaveBeenCalled();
  });

  it("disables the input and send button when disabled", () => {
    renderComposer({
      disabled: true,
    });

    expect(screen.getByRole("textbox")).toBeDisabled();
    expect(
      screen.getByRole("button", { name: "Send message" }),
    ).toBeDisabled();
  });

  it("disables the send button when the value is empty", () => {
    renderComposer({
      value: "",
      disabled: false,
    });

    expect(
      screen.getByRole("button", { name: "Send message" }),
    ).toBeDisabled();
  });

  it("disables the send button when the value contains only whitespace", () => {
    renderComposer({
      value: "   ",
      disabled: false,
    });

    expect(
      screen.getByRole("button", { name: "Send message" }),
    ).toBeDisabled();
  });

  it("enables the send button when the value contains text", () => {
    renderComposer({
      value: "Hello",
      disabled: false,
    });

    expect(
      screen.getByRole("button", { name: "Send message" }),
    ).toBeEnabled();
  });

  it("does not send when the form is submitted with an empty value", async () => {
    const user = userEvent.setup();
    const onSend = vi.fn();

    renderComposer({
      value: "",
      onSend,
    });

    const sendButton = screen.getByRole("button", {
      name: "Send message",
    });

    expect(sendButton).toBeDisabled();

    await user.click(sendButton);

    expect(onSend).not.toHaveBeenCalled();
  });

  it("does not send when disabled even if the input has text", async () => {
    const onSend = vi.fn();

    renderComposer({
      value: "Hello tutor",
      disabled: true,
      onSend,
    });

    expect(screen.getByRole("textbox")).toBeDisabled();
    expect(onSend).not.toHaveBeenCalled();
  });

  it("sets the maximum input length to 20000 characters", () => {
    renderComposer();

    const input = screen.getByRole("textbox");

    expect(input).toHaveAttribute("maxlength", "20000");
  });
});
