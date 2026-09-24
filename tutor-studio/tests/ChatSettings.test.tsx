
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

  async function openSettings() {
    const user = userEvent.setup();

    await user.click(
      screen.getByText("Conversation settings"),
    );
  }

  it("renders the conversation settings section", () => {
    renderSettings();

    expect(
      screen.getByText("Conversation settings"),
    ).toBeInTheDocument();
  });

  it("shows the settings content when opened", async () => {
    renderSettings();

    await openSettings();

    expect(screen.getByText("Tutor knowledge")).toBeInTheDocument();

    expect(
      screen.getByText("Enabled by tutor configuration"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Retrieval depth"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Show sources"),
    ).toBeInTheDocument();
  });

  it("displays the current topK value", async () => {
    renderSettings({
      topK: 7,
    });

    await openSettings();

    expect(screen.getByText("Top 7")).toBeInTheDocument();
  });

  it("configures the slider with a minimum value of 1", async () => {
    renderSettings();

    await openSettings();

    const slider = screen.getByRole("slider");

    expect(slider).toHaveAttribute("aria-valuemin", "1");
  });

  it("configures the slider with a maximum value of 10", async () => {
    renderSettings();

    await openSettings();

    const slider = screen.getByRole("slider");

    expect(slider).toHaveAttribute("aria-valuemax", "10");
  });

  it("sets the slider to the current topK value", async () => {
    renderSettings({
      topK: 6,
    });

    await openSettings();

    const slider = screen.getByRole("slider");

    expect(slider).toHaveAttribute("aria-valuenow", "6");
  });

  it("calls onTopKChange when the slider changes", async () => {
    const onTopKChange = vi.fn();

    renderSettings({
      topK: 5,
      onTopKChange,
    });

    await openSettings();

    const slider = screen.getByRole("slider");

    fireEvent.change(slider, {
      target: {
        value: "6",
      },
    });

    expect(onTopKChange).toHaveBeenCalledTimes(1);
    expect(onTopKChange).toHaveBeenCalledWith(6);
  });

  it("updates the displayed topK when the slider changes", async () => {
    renderControlledSettings({
      topK: 5,
    });

    await openSettings();

    const slider = screen.getByRole("slider");

    fireEvent.change(slider, {
      target: {
        value: "6",
      },
    });

    expect(screen.getByText("Top 6")).toBeInTheDocument();
  });

  it("renders the Show sources switch", async () => {
    renderSettings();

    await openSettings();

    expect(screen.getByText("Show sources")).toBeInTheDocument();
    expect(screen.getByRole("switch")).toBeInTheDocument();
  });

  it("shows the switch as checked when showSources is true", async () => {
    renderSettings({
      showSources: true,
    });

    await openSettings();

    expect(screen.getByRole("switch")).toBeChecked();
  });

  it("shows the switch as unchecked when showSources is false", async () => {
    renderSettings({
      showSources: false,
    });

    await openSettings();

    expect(screen.getByRole("switch")).not.toBeChecked();
  });

  it("calls onShowSourcesChange when the switch is toggled on", async () => {
    const user = userEvent.setup();
    const onShowSourcesChange = vi.fn();

    renderControlledSettings({
      showSources: false,
      onShowSourcesChange,
    });

    await openSettings();

    const switchElement = screen.getByRole("switch");

    await user.click(switchElement);

    expect(onShowSourcesChange).toHaveBeenCalledTimes(1);
    expect(onShowSourcesChange).toHaveBeenLastCalledWith(true);
  });

  it("calls onShowSourcesChange when the switch is toggled off", async () => {
    const user = userEvent.setup();
    const onShowSourcesChange = vi.fn();

    renderControlledSettings({
      showSources: true,
      onShowSourcesChange,
    });

    await openSettings();

    const switchElement = screen.getByRole("switch");

    await user.click(switchElement);

    expect(onShowSourcesChange).toHaveBeenCalledTimes(1);
    expect(onShowSourcesChange).toHaveBeenLastCalledWith(false);
  });

  it("updates the switch when showSources changes", async () => {
    const user = userEvent.setup();

    renderControlledSettings({
      showSources: false,
    });

    await openSettings();

    const switchElement = screen.getByRole("switch");

    expect(switchElement).not.toBeChecked();

    await user.click(switchElement);

    expect(switchElement).toBeChecked();
  });

  it("does not call callbacks when initially rendered", () => {
    const onTopKChange = vi.fn();
    const onShowSourcesChange = vi.fn();

    renderSettings({
      onTopKChange,
      onShowSourcesChange,
    });

    expect(onTopKChange).not.toHaveBeenCalled();
    expect(onShowSourcesChange).not.toHaveBeenCalled();
  });
});
