import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import TutorSelector from "../src/features/user/components/TutorSelector";

describe("TutorSelector", () => {
  const tutors = [
    {
      tutorId: "tutor-1",
      tutorName: "React Tutor",
    },
    {
      tutorId: "tutor-2",
      tutorName: "TypeScript Tutor",
    },
    {
      tutorId: "tutor-3",
      tutorName: "Python Tutor",
    },
  ];

  function renderSelector(
    overrides: Partial<React.ComponentProps<typeof TutorSelector>> = {},
  ) {
    const props = {
      tutors,
      selectedTutorId: "tutor-1",
      disabled: false,
      onChange: vi.fn(),
      ...overrides,
    };

    return {
      ...render(<TutorSelector {...props} />),
      props,
    };
  }

  async function openSelector() {
    const user = userEvent.setup();

    await user.click(
      screen.getByRole("combobox"),
    );
  }

  it("renders the Active tutor label", () => {
    renderSelector();

    expect(
      screen.getByText("Active tutor"),
    ).toBeInTheDocument();
  });

  it("renders the tutor selector", () => {
    renderSelector();

    expect(
      screen.getByRole("combobox"),
    ).toBeInTheDocument();
  });

  it("renders the placeholder when no tutor is selected", () => {
    renderSelector({
      selectedTutorId: undefined,
    });

    expect(
      screen.getByText("Select an active tutor"),
    ).toBeInTheDocument();
  });

  it("renders the selected tutor", () => {
    renderSelector({
      selectedTutorId: "tutor-1",
    });

    expect(
      screen.getByText("React Tutor"),
    ).toBeInTheDocument();
  });

  it("renders all tutor options when opened", async () => {
    renderSelector();

    await openSelector();

    expect(
      screen.getAllByText("React Tutor").length,
    ).toBeGreaterThanOrEqual(2);

    expect(
      screen.getByText("TypeScript Tutor"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Python Tutor"),
    ).toBeInTheDocument();
  });

  it("uses tutorId as the option value", async () => {
    renderSelector({
      selectedTutorId: undefined,
    });

    await openSelector();

    const options = screen.getAllByRole("option");

    expect(
      options.find(
        (option) =>
          option.getAttribute("aria-label") ===
          "TypeScript Tutor",
      ),
    ).toBeInTheDocument();
  });

  it("calls onChange when a tutor is selected", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();

    renderSelector({
      selectedTutorId: undefined,
      onChange,
    });

    await openSelector();

    await user.click(
      screen.getByText("TypeScript Tutor"),
    );

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith(
      "tutor-2",
      expect.anything(),
    );
  });

  it("disables the selector when disabled is true", () => {
    renderSelector({
      disabled: true,
    });

    expect(
      screen.getByRole("combobox"),
    ).toBeDisabled();
  });

  it("enables the selector when disabled is false", () => {
    renderSelector({
      disabled: false,
    });

    expect(
      screen.getByRole("combobox"),
    ).not.toBeDisabled();
  });

  it("renders with the runtime tutor selector CSS class", () => {
    const { container } = renderSelector();

    expect(
      container.querySelector(".runtime-tutor-selector"),
    ).toBeInTheDocument();
  });

  it("configures the selector for searching", () => {
    renderSelector();

    const combobox = screen.getByRole("combobox");

    expect(combobox).toHaveAttribute(
      "aria-expanded",
      "false",
    );

    expect(combobox).toHaveAttribute(
      "aria-autocomplete",
      "list",
    );
  });

  it("renders correctly with an empty tutor list", async () => {
    renderSelector({
      tutors: [],
      selectedTutorId: undefined,
    });

    expect(
      screen.getByRole("combobox"),
    ).toBeInTheDocument();

    await openSelector();

    expect(
      screen.queryByText("React Tutor"),
    ).not.toBeInTheDocument();

    expect(
      screen.queryByText("TypeScript Tutor"),
    ).not.toBeInTheDocument();

    expect(
      screen.queryByText("Python Tutor"),
    ).not.toBeInTheDocument();
  });
});
