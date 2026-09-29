
import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";

import ChatSettings from "../src/features/user/components/ChatSettings";

// Mock only the Slider.
// The real Ant Design Slider/rc-slider does not reliably respond to
// synthetic keyboard events in jsdom, so the tests below focus on whether
// ChatSettings configures and handles the Slider correctly.
vi.mock("antd", async () => {
  const actual = await vi.importActual<typeof import("antd")>("antd");

  return {
    ...actual,
    Slider: ({
      min,
      max,
      value,
      onChange,
    }: {
      min: number;
      max: number;
      value: number;
      onChange: (value: number) => void;
    }) => (
      <input
        type="range"
        aria-label="Retrieval depth"
        aria-valuemin={min}
        aria-valuemax={max}
        aria-valuenow={value}
        min={min}
        max={max}
        value={value}
        onChange={(event) => {
          onChange(Number(event.target.value));
        }}
        data-testid="retrieval-slider"
      />
    ),
  };
});

describe("ChatSettings", () => {
  function renderSettings(
    overrides: Partial<React.ComponentProps<typeof ChatSettings>> = {},
  ) {
    const props = {
      topK: 5,
      showSources: true,
      onTopKChange: vi.fn(),
      onShowSourcesChange: vi.fn(),
      ...overrides,
    };

    return {
      ...render(<ChatSettings {...props} />),
      props,
    };
  }

  function renderControlledSettings(
    overrides: Partial<React.ComponentProps<typeof ChatSettings>> = {},
  ) {
    const onTopKChange = overrides.onTopKChange ?? vi.fn();
    const onShowSourcesChange =
      overrides.onShowSourcesChange ?? vi.fn();

    function ControlledSettings() {
      const [topK, setTopK] = useState(overrides.topK ?? 5);
      const [showSources, setShowSources] = useState(
        overrides.showSources ?? true,
      );

      return (
        <ChatSettings
          topK={topK}
          showSources={showSources}
          onTopKChange={(value) => {
            setTopK(value);
            onTopKChange(value);
          }}
          onShowSourcesChange={(checked) => {
            setShowSources(checked);
            onShowSourcesChange(checked);
          }}
        />
      );
    }

    return {
      ...render(<ControlledSettings />),
      onTopKChange,
      onShowSourcesChange,
    };
  }

  it("renders the settings controls for its enclosing settings drawer", () => {
    renderSettings();
    expect(screen.getByText("Tutor knowledge")).toBeInTheDocument();
    expect(screen.getByText("Enabled by tutor configuration")).toBeInTheDocument();
    expect(screen.getByText("Retrieval depth")).toBeInTheDocument();
    expect(screen.getByText("Show sources")).toBeInTheDocument();
  });

  it("displays and configures the current retrieval depth", () => {
    renderSettings({ topK: 7 });
    expect(screen.getByText("Top 7")).toBeInTheDocument();
    const slider = screen.getByRole("slider");
    expect(slider).toHaveAttribute("aria-valuemin", "1");
    expect(slider).toHaveAttribute("aria-valuemax", "10");
    expect(slider).toHaveAttribute("aria-valuenow", "7");
  });

  it("calls onTopKChange and renders its updated value", () => {
    const onTopKChange = vi.fn();
    renderControlledSettings({ topK: 5, onTopKChange });
    fireEvent.change(screen.getByRole("slider"), { target: { value: "6" } });
    expect(onTopKChange).toHaveBeenCalledWith(6);
    expect(screen.getByText("Top 6")).toBeInTheDocument();
  });

  it("reflects and updates the sources setting", async () => {
    const user = userEvent.setup();
    const onShowSourcesChange = vi.fn();
    renderControlledSettings({ showSources: false, onShowSourcesChange });
    const [switchElement] = screen.getAllByRole("switch");
    expect(switchElement).not.toBeChecked();
    await user.click(switchElement);
    expect(onShowSourcesChange).toHaveBeenCalledWith(true);
    expect(switchElement).toBeChecked();
  });

  it("does not call callbacks when initially rendered", () => {
    const onTopKChange = vi.fn();
    const onShowSourcesChange = vi.fn();
    renderSettings({ onTopKChange, onShowSourcesChange });
    expect(onTopKChange).not.toHaveBeenCalled();
    expect(onShowSourcesChange).not.toHaveBeenCalled();
  });
});
