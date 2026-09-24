import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import TutorOverviewPanel from "../src/features/editor/components/TutorOverviewPanel";

// Mock react-router-dom
const mockNavigate = vi.fn();

vi.mock("react-router-dom", () => ({
  useNavigate: () => mockNavigate,
}));

// Mock StatusTag
vi.mock("../src/components/StatusTag", () => ({
  default: ({ value }: { value: string }) => (
    <span data-testid="status-tag">{value}</span>
  ),
}));

// Mock tutor utilities
vi.mock("../src/features/editor/utils/tutorUtils.js", () => ({
  formatDateTime: vi.fn((date: string) => `Formatted ${date}`),
  revisionStatus: vi.fn((status: string) => status),
  tutorStatus: vi.fn((status: string) => status),
}));

describe("TutorOverviewPanel", () => {
  const tutor = {
    id: "tutor-123",
    name: "Robotics Tutor",
    description: "A tutor for robotics students.",
    status: "Active",
    createdUtc: "2026-09-01T10:00:00Z",
    modifiedUtc: "2026-09-02T10:00:00Z",
    activeRevisionId: "revision-1",
  };

  const prompts = [
    {
      id: "prompt-1",
      versionNumber: 1,
      name: "Robotics Guide",
    },
  ];

  const revisions = [
    {
      id: "revision-1",
      versionNumber: 2,
      status: "Active",
      promptVersionId: "prompt-1",
      promptVersionNumber: 1,
      promptVersionName: "Robotics Guide",
      interactionModelId: "model-1",
      interactionModelName: "Socratic",
      providerModelName: "GPT Model",
      activationSlug: "robotics-v2",
      latestSnapshotId: "snapshot-1",
    },
  ];

  const defaultProps = {
    tutor,
    prompts,
    revisions,
    canManage: true,
    onOpenTab: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the tutor information", () => {
    render(<TutorOverviewPanel {...defaultProps} />);

    expect(screen.getByText("Tutor")).toBeInTheDocument();
    expect(screen.getByText("Robotics Tutor")).toBeInTheDocument();
    expect(
      screen.getByText("A tutor for robotics students.")
    ).toBeInTheDocument();

    expect(screen.getByText("Formatted 2026-09-01T10:00:00Z"))
      .toBeInTheDocument();

    expect(screen.getByText("Formatted 2026-09-02T10:00:00Z"))
      .toBeInTheDocument();
  });

  it("renders the tutor status", () => {
    render(<TutorOverviewPanel {...defaultProps} />);

    expect(screen.getByTestId("status-tag")).toHaveTextContent("Active");
  });

  it("renders the current runtime configuration", () => {
    render(<TutorOverviewPanel {...defaultProps} />);

    expect(
      screen.getByText("Current runtime configuration")
    ).toBeInTheDocument();

    expect(screen.getByText("Revision 2")).toBeInTheDocument();
    expect(screen.getByText("v1 · Robotics Guide")).toBeInTheDocument();
    expect(screen.getByText("Socratic")).toBeInTheDocument();
    expect(screen.getByText("GPT Model")).toBeInTheDocument();
    expect(screen.getByText("robotics-v2")).toBeInTheDocument();
    expect(screen.getByText("Captured")).toBeInTheDocument();
  });

  it("renders the setup checklist", () => {
    render(<TutorOverviewPanel {...defaultProps} />);

    expect(screen.getByText("Setup checklist")).toBeInTheDocument();

    expect(screen.getByText("Tutor created")).toBeInTheDocument();
    expect(screen.getByText("Prompt version created")).toBeInTheDocument();
    expect(
      screen.getByText("Interaction model selected")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Draft revision configured")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Revision published")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Revision activated")
    ).toBeInTheDocument();
    expect(
      screen.getByText("Ready for benchmarking")
    ).toBeInTheDocument();
  });

  it("marks completed checklist items as complete", () => {
    render(<TutorOverviewPanel {...defaultProps} />);

    const completeItems = screen.getAllByText("Complete");

    expect(completeItems.length).toBeGreaterThan(0);
  });

  it("does not show action buttons for completed checklist items", () => {
    render(<TutorOverviewPanel {...defaultProps} />);

    expect(
      screen.queryByRole("button", { name: "Create prompt" })
    ).not.toBeInTheDocument();

    expect(
      screen.queryByRole("button", { name: "Configure revision" })
    ).not.toBeInTheDocument();

    expect(
      screen.queryByRole("button", { name: "Publish revision" })
    ).not.toBeInTheDocument();
  });

  it("shows no active revision when one is not configured", () => {
    const props = {
      ...defaultProps,
      tutor: {
        ...tutor,
        activeRevisionId: "missing-revision",
      },
      revisions: [],
    };

    render(<TutorOverviewPanel {...props} />);

    expect(
      screen.getByText("No revision is active yet.")
    ).toBeInTheDocument();
  });

  it("shows missing snapshot when the active revision has no snapshot", () => {
    const props = {
      ...defaultProps,
      revisions: [
        {
          ...revisions[0],
          latestSnapshotId: null,
        },
      ],
    };

    render(<TutorOverviewPanel {...props} />);

    expect(screen.getByText("Missing")).toBeInTheDocument();
  });

  it("shows action buttons when configuration is incomplete", () => {
    const incompleteRevisions = [
      {
        id: "revision-1",
        versionNumber: 1,
        status: "Draft",
        promptVersionId: null,
        interactionModelId: null,
        latestSnapshotId: null,
      },
    ];

    render(
      <TutorOverviewPanel
        {...defaultProps}
        prompts={[]}
        revisions={incompleteRevisions}
      />
    );

    expect(
      screen.getByRole("button", { name: "Create prompt" })
    ).toBeInTheDocument();

    expect(
    screen.getAllByRole("button", { name: "Configure revision" })
    ).toHaveLength(2);
  });

  it("calls onOpenTab when a checklist action is clicked", async () => {
    const user = userEvent.setup();

    const incompleteRevisions = [
      {
        id: "revision-1",
        versionNumber: 1,
        status: "Draft",
        promptVersionId: null,
        interactionModelId: null,
        latestSnapshotId: null,
      },
    ];

    const onOpenTab = vi.fn();

    render(
      <TutorOverviewPanel
        {...defaultProps}
        prompts={[]}
        revisions={incompleteRevisions}
        onOpenTab={onOpenTab}
      />
    );

    await user.click(
      screen.getByRole("button", { name: "Create prompt" })
    );

    expect(onOpenTab).toHaveBeenCalledWith("prompts");
  });

  it("does not show management action buttons when canManage is false", () => {
    const incompleteRevisions = [
      {
        id: "revision-1",
        versionNumber: 1,
        status: "Draft",
        promptVersionId: null,
        interactionModelId: null,
        latestSnapshotId: null,
      },
    ];

    render(
      <TutorOverviewPanel
        {...defaultProps}
        canManage={false}
        prompts={[]}
        revisions={incompleteRevisions}
      />
    );

    expect(
      screen.queryByRole("button", { name: "Create prompt" })
    ).not.toBeInTheDocument();

    expect(
      screen.queryByRole("button", { name: "Configure revision" })
    ).not.toBeInTheDocument();
  });

  it("shows the Benchmark button when the tutor is ready", () => {
    render(<TutorOverviewPanel {...defaultProps} />);

    expect(
      screen.getByRole("button", { name: /Benchmark/i })
    ).toBeInTheDocument();
  });

  it("does not show the Benchmark button when the tutor is not ready", () => {
    const props = {
      ...defaultProps,
      revisions: [
        {
          ...revisions[0],
          latestSnapshotId: null,
        },
      ],
    };

    render(<TutorOverviewPanel {...props} />);

    expect(
      screen.queryByRole("button", { name: /Benchmark/i })
    ).not.toBeInTheDocument();
  });

  it("navigates to benchmarking when Benchmark is clicked", async () => {
    const user = userEvent.setup();

    render(<TutorOverviewPanel {...defaultProps} />);

    await user.click(
      screen.getByRole("button", { name: /Benchmark/i })
    );

    expect(mockNavigate).toHaveBeenCalledWith(
      "/benchmarking?tab=new&tutorId=tutor-123&revisionId=revision-1"
    );
  });

  it("uses the active status revision when activeRevisionId does not match", () => {
    const props = {
      ...defaultProps,
      tutor: {
        ...tutor,
        activeRevisionId: "missing-revision",
      },
    };

    render(<TutorOverviewPanel {...props} />);

    expect(screen.getByText("Revision 2")).toBeInTheDocument();
  });

  it("shows no description when the tutor has no description", () => {
    render(
      <TutorOverviewPanel
        {...defaultProps}
        tutor={{
          ...tutor,
          description: "",
        }}
      />
    );

    expect(
      screen.getByText("No description provided.")
    ).toBeInTheDocument();
  });
});
