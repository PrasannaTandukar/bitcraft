import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import TutorRuntimeHeader from "../src/features/user/components/TutorRuntimeHeader";

describe("TutorRuntimeHeader", () => {
  const tutor = {
    tutorName: "React Tutor",
    revisionNumber: 3,
    description: "A tutor for learning React and TypeScript.",
    model: {
      name: "GPT-4",
    },
    embeddingModel: {
      name: "text-embedding-3-small",
    },
    knowledgeDocumentCount: 5,
  };

  function renderHeader(
    overrides: Partial<React.ComponentProps<typeof TutorRuntimeHeader>> = {},
  ) {
    const props = {
      tutor,
      canManage: false,
      onConfigure: vi.fn(),
      onBenchmark: vi.fn(),
      ...overrides,
    };

    return {
      ...render(<TutorRuntimeHeader {...props} />),
      props,
    };
  }

  it("renders nothing when tutor is not provided", () => {
    const { container } = render(
      <TutorRuntimeHeader
        tutor={null}
        canManage={false}
        onConfigure={vi.fn()}
        onBenchmark={vi.fn()}
      />,
    );

    expect(container.firstChild).toBeNull();
  });

  it("renders the tutor name", () => {
    renderHeader();

    expect(
      screen.getByRole("heading", {
        name: "React Tutor",
      }),
    ).toBeInTheDocument();
  });

  it("renders the active revision number", () => {
    renderHeader();

    expect(
      screen.getByText("Active · Revision 3"),
    ).toBeInTheDocument();
  });

  it("renders the tutor description", () => {
    renderHeader();

    expect(
      screen.getByText(
        "A tutor for learning React and TypeScript.",
      ),
    ).toBeInTheDocument();
  });

  it("does not render the description when it is missing", () => {
    renderHeader({
      tutor: {
        ...tutor,
        description: "",
      },
    });

    expect(
      screen.queryByText(
        "A tutor for learning React and TypeScript.",
      ),
    ).not.toBeInTheDocument();
  });

  it("does not render management buttons when canManage is false", () => {
    renderHeader({
      canManage: false,
    });

    expect(
      screen.queryByRole("button", {
        name: /Configure/i,
      }),
    ).not.toBeInTheDocument();

    expect(
      screen.queryByRole("button", {
        name: /Benchmark/i,
      }),
    ).not.toBeInTheDocument();
  });

  it("renders management buttons when canManage is true", () => {
    renderHeader({
      canManage: true,
    });

    expect(
      screen.getByRole("button", {
        name: /Configure/i,
      }),
    ).toBeInTheDocument();

    expect(
      screen.getByRole("button", {
        name: /Benchmark/i,
      }),
    ).toBeInTheDocument();
  });

  it("calls onConfigure when Configure is clicked", async () => {
    const user = userEvent.setup();
    const onConfigure = vi.fn();

    renderHeader({
      canManage: true,
      onConfigure,
    });

    await user.click(
      screen.getByRole("button", {
        name: /Configure/i,
      }),
    );

    expect(onConfigure).toHaveBeenCalledTimes(1);
  });

  it("calls onBenchmark when Benchmark is clicked", async () => {
    const user = userEvent.setup();
    const onBenchmark = vi.fn();

    renderHeader({
      canManage: true,
      onBenchmark,
    });

    await user.click(
      screen.getByRole("button", {
        name: /Benchmark/i,
      }),
    );

    expect(onBenchmark).toHaveBeenCalledTimes(1);
  });

  it("renders the interaction model", () => {
    renderHeader();

    expect(
      screen.getByText("GPT-4"),
    ).toBeInTheDocument();
  });

  it("renders the query embedding model", () => {
    renderHeader();

    expect(
      screen.getByText("text-embedding-3-small"),
    ).toBeInTheDocument();
  });

  it("renders Not required when there is no embedding model", () => {
    renderHeader({
      tutor: {
        ...tutor,
        embeddingModel: null,
      },
    });

    expect(
      screen.getByText("Not required"),
    ).toBeInTheDocument();
  });

  it("renders the singular document label", () => {
    renderHeader({
      tutor: {
        ...tutor,
        knowledgeDocumentCount: 1,
      },
    });

    expect(
      screen.getByText("1 document"),
    ).toBeInTheDocument();
  });

  it("renders the plural document label", () => {
    renderHeader({
      tutor: {
        ...tutor,
        knowledgeDocumentCount: 5,
      },
    });

    expect(
      screen.getByText("5 documents"),
    ).toBeInTheDocument();
  });

  it("renders zero documents using the plural form", () => {
    renderHeader({
      tutor: {
        ...tutor,
        knowledgeDocumentCount: 0,
      },
    });

    expect(
      screen.getByText("0 documents"),
    ).toBeInTheDocument();
  });

  it("renders the expected runtime header CSS class", () => {
    const { container } = renderHeader();

    expect(
      container.querySelector(".runtime-header-card"),
    ).toBeInTheDocument();
  });

  it("does not call management callbacks during initial render", () => {
    const onConfigure = vi.fn();
    const onBenchmark = vi.fn();

    renderHeader({
      canManage: true,
      onConfigure,
      onBenchmark,
    });

    expect(onConfigure).not.toHaveBeenCalled();
    expect(onBenchmark).not.toHaveBeenCalled();
  });
});
