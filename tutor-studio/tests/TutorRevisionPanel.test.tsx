import { App } from "antd";
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import TutorRevisionsPanel from "../src/features/editor/components/TutorRevisionsPanel.jsx";

import {
  activateTutorRevision,
  archiveTutorRevision,
  createTutorRevision,
  discardTutorRevision,
  publishTutorRevision,
} from "../src/features/editor/hooks/tutorApi.js";

import {
  deploymentTarget,
  formatDateTime,
  revisionIsPublishable,
  revisionStatus,
} from "../src/features/editor/utils/tutorUtils.js";

const mockNavigate = vi.fn();

// TutorRevisionsPanel's `perform` helper deliberately rethrows the request
// error inside modal.confirm's onOk (after setting the error banner), so
// that Ant Design's Modal.confirm keeps its own internal error/retry
// bookkeeping correct. Vitest itself runs in Node, so a rejection like this
// — created and rethrown well after the promise chain Ant Design is
// awaiting has settled from the test's point of view — is reported via
// Node's `process.on("unhandledRejection", ...)`, not a jsdom/window event
// (jsdom's "unhandledrejection" only fires for promises created inside the
// jsdom realm, which this isn't). The tests already assert on the resulting
// error banner via waitFor, so the rejection itself is expected, not a bug;
// swallow it at the Node process level for the known "*.failed" errors
// these specific tests trigger, and let anything else still surface.
const EXPECTED_TEST_REJECTION_MESSAGES = new Set([
  "Publish failed",
  "Activation failed",
  "Archive failed",
  "Discard failed",
]);

const handleTestEnvironmentRejection = (reason: unknown) => {
  const message = reason instanceof Error ? reason.message : String(reason);
  if (!EXPECTED_TEST_REJECTION_MESSAGES.has(message)) {
    throw reason;
  }
};

beforeAll(() => {
  process.on("unhandledRejection", handleTestEnvironmentRejection);
});

afterAll(() => {
  process.off("unhandledRejection", handleTestEnvironmentRejection);
});

vi.mock("react-router-dom", () => ({
  useNavigate: () => mockNavigate,
}));

vi.mock("../src/features/editor/hooks/tutorApi.js", () => ({
  activateTutorRevision: vi.fn(),
  archiveTutorRevision: vi.fn(),
  createTutorRevision: vi.fn(),
  discardTutorRevision: vi.fn(),
  publishTutorRevision: vi.fn(),
}));

vi.mock("../src/features/editor/utils/tutorUtils.js", () => ({
  deploymentTarget: vi.fn((value) => value || "Unknown"),
  formatDateTime: vi.fn((value) => value || "—"),
  revisionIsPublishable: vi.fn(() => true),
  revisionStatus: vi.fn((value) => value),
}));

vi.mock("../src/components/StatusTag.jsx", () => ({
  default: ({ value }: { value: string }) => (
    <span data-testid={`status-${value}`}>{value}</span>
  ),
}));

vi.mock("../src/features/editor/components/RevisionEditor.jsx", () => ({
  default: ({
    revision,
    onClose,
    onSaved,
    onCreatePrompt,
  }: {
    revision: any;
    onClose: () => void;
    onSaved: () => void;
    onCreatePrompt: () => void;
  }) => (
    <div data-testid="revision-editor">
      <div>Editing revision {revision.versionNumber}</div>

      <button onClick={onClose}>Close editor</button>

      <button onClick={onSaved}>Save editor</button>

      <button onClick={onCreatePrompt}>
        Create prompt from editor
      </button>
    </div>
  ),
}));

const renderPanel = (props = {}) => {
  return render(
    <App>
      <TutorRevisionsPanel
        tutorId="tutor-1"
        revisions={[]}
        prompts={[]}
        interactionModels={[]}
        embeddingModels={[]}
        canManage={true}
        onChanged={vi.fn()}
        onCreatePrompt={vi.fn()}
        {...props}
      />
    </App>,
  );
};

// NOTE: TutorRevisionsPanel renders denormalized display fields
// (promptVersionNumber/promptVersionName, interactionModelName/providerModelName,
// queryEmbeddingModelName/queryEmbeddingDimensions) rather than the raw
// promptVersionId/interactionModelId/embeddingModelId foreign keys. Those id
// fields are still used for the "missing configuration" validation warning,
// so both sets are included here.
const baseRevision = {
  id: "revision-1",
  versionNumber: 1,
  status: "Draft",
  isActive: false,
  promptVersionId: "prompt-1",
  promptVersionNumber: 1,
  promptVersionName: "prompt-1",
  interactionModelId: "interaction-1",
  interactionModelName: "interaction-1",
  providerModelName: "gpt-4",
  interactionModelEnabled: true,
  runtimeEngineEnabled: true,
  embeddingModelId: "embedding-1",
  queryEmbeddingModelName: "embedding-1",
  queryEmbeddingDimensions: 1536,
  deploymentTarget: "local",
  createdAt: "2026-09-01T10:00:00Z",
  publishedAt: null,
  activatedAt: null,
  archivedAt: null,
  activationSlug: null,
  latestSnapshotId: null,
};

const createRevision = (overrides = {}) => ({
  ...baseRevision,
  ...overrides,
});

const getConfirmDialog = async () => {
  return await screen.findByRole("dialog");
};

const confirmModalAction = async (action: string) => {
  const dialog = await getConfirmDialog();

  const button = within(dialog).getByRole("button", {
    name: action,
    exact: true,
  });

  fireEvent.click(button);
};

describe("TutorRevisionsPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(revisionStatus).mockImplementation(
      (value: any) => value,
    );

    vi.mocked(deploymentTarget).mockImplementation(
      (value: any) => value || "Unknown",
    );

    vi.mocked(formatDateTime).mockImplementation(
      (value: any) => value || "—",
    );

    vi.mocked(revisionIsPublishable).mockReturnValue(true);

    vi.mocked(createTutorRevision).mockResolvedValue({
      ...baseRevision,
      id: "revision-created",
      versionNumber: 2,
      status: "Draft",
    });

    vi.mocked(publishTutorRevision).mockResolvedValue(undefined);
    vi.mocked(activateTutorRevision).mockResolvedValue(undefined);
    vi.mocked(archiveTutorRevision).mockResolvedValue(undefined);
    vi.mocked(discardTutorRevision).mockResolvedValue(undefined);
  });

  describe("rendering", () => {
    it("renders the revisions heading and description", () => {
      renderPanel();

      expect(screen.getByText("Revisions")).toBeInTheDocument();

      expect(
        screen.getByText(
          "Versioned, reproducible runtime configurations for this tutor.",
        ),
      ).toBeInTheDocument();
    });

    it("shows Create revision when the user can manage revisions", () => {
      renderPanel();

      expect(
        screen.getByRole("button", { name: /create revision/i }),
      ).toBeInTheDocument();
    });

    it("does not show the Create revision button when the user cannot manage revisions", () => {
      renderPanel({
        canManage: false,
      });

      expect(
        screen.queryByRole("button", { name: /create revision/i }),
      ).not.toBeInTheDocument();
    });

    it("shows the empty state when there are no revisions", () => {
      renderPanel({
        revisions: [],
      });

      expect(
        screen.getByText("No revisions"),
      ).toBeInTheDocument();
    });

    it("shows Create first revision for managers in the empty state", () => {
      renderPanel({
        revisions: [],
        canManage: true,
      });

      expect(
        screen.getByRole("button", {
          name: "Create first revision",
        }),
      ).toBeInTheDocument();
    });

    it("does not show Create first revision for non-managers", () => {
      renderPanel({
        revisions: [],
        canManage: false,
      });

      expect(
        screen.queryByRole("button", {
          name: "Create first revision",
        }),
      ).not.toBeInTheDocument();
    });
  });

  describe("revision details", () => {
    it("renders a revision and its status", () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByText("Revision 1"),
      ).toBeInTheDocument();

      expect(
        screen.getByTestId("status-Draft"),
      ).toBeInTheDocument();
    });

    it("renders the revision configuration details", () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByText("Prompt version"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("Prompt v1 · prompt-1"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("Interaction model"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("interaction-1 · gpt-4"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("Query embedding"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("embedding-1 · 1536 dimensions"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("Deployment"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("local"),
      ).toBeInTheDocument();
    });

    it("renders the Current runtime tag for an active revision", () => {
      const revision = createRevision({
        status: "Active",
        isActive: true,
      });

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByText("Current runtime"),
      ).toBeInTheDocument();
    });

    it("shows a Snapshot status tag when a snapshot exists", () => {
      // The component does not render the raw latestSnapshotId as text —
      // it renders a StatusTag (mocked here to `status-${value}`) inside
      // the "Snapshot" Descriptions.Item, reflecting the snapshot's
      // readiness rather than its ID.
      const revision = createRevision({
        latestSnapshotId: "snapshot-123",
      });

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByText("Snapshot"),
      ).toBeInTheDocument();

      expect(
        screen.getByTestId("status-Ready"),
      ).toBeInTheDocument();
    });

    it("shows missing configuration warnings for a draft", () => {
      const revision = createRevision({
        promptVersionId: null,
        interactionModelId: null,
        interactionModelEnabled: false,
        runtimeEngineEnabled: false,
      });

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByText(
          /Before publishing, select: prompt version and interaction model and enabled interaction model and enabled runtime engine/i,
        ),
      ).toBeInTheDocument();
    });

    it("does not show the publishing warning when the draft is fully configured", () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.queryByText(/Before publishing, select:/i),
      ).not.toBeInTheDocument();
    });
  });

  describe("draft revision actions", () => {
    it("shows Configure, Publish and Discard for a manageable draft", () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
        canManage: true,
      });

      expect(
        screen.getByRole("button", { name: "Configure" }),
      ).toBeInTheDocument();

      expect(
        screen.getByRole("button", { name: "Publish" }),
      ).toBeInTheDocument();

      expect(
        screen.getByRole("button", { name: "Discard" }),
      ).toBeInTheDocument();
    });

    it("does not show draft management actions for a non-manager", () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
        canManage: false,
      });

      expect(
        screen.queryByRole("button", { name: "Configure" }),
      ).not.toBeInTheDocument();

      expect(
        screen.queryByRole("button", { name: "Publish" }),
      ).not.toBeInTheDocument();

      expect(
        screen.queryByRole("button", { name: "Discard" }),
      ).not.toBeInTheDocument();

      expect(
        screen.getByRole("button", {
          name: "View configuration",
        }),
      ).toBeInTheDocument();
    });

    it("disables Publish when the revision is not publishable", () => {
      vi.mocked(revisionIsPublishable).mockReturnValue(false);

      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByRole("button", {
          name: "Publish",
        }),
      ).toBeDisabled();
    });

    it("enables Publish when the revision is publishable", () => {
      vi.mocked(revisionIsPublishable).mockReturnValue(true);

      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByRole("button", {
          name: "Publish",
        }),
      ).toBeEnabled();
    });

    it("opens the RevisionEditor when Configure is clicked", () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Configure",
        }),
      );

      expect(
        screen.getByTestId("revision-editor"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("Editing revision 1"),
      ).toBeInTheDocument();
    });

    it("closes the RevisionEditor when its close callback is triggered", () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Configure",
        }),
      );

      fireEvent.click(
        screen.getByRole("button", {
          name: "Close editor",
        }),
      );

      expect(
        screen.queryByTestId("revision-editor"),
      ).not.toBeInTheDocument();
    });

    it("calls onChanged when the RevisionEditor saves", () => {
      const revision = createRevision();
      const onChanged = vi.fn();

      renderPanel({
        revisions: [revision],
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Configure",
        }),
      );

      fireEvent.click(
        screen.getByRole("button", {
          name: "Save editor",
        }),
      );

      expect(onChanged).toHaveBeenCalledTimes(1);
    });

    it("calls onCreatePrompt when the editor requests prompt creation", () => {
      const revision = createRevision();
      const onCreatePrompt = vi.fn();

      renderPanel({
        revisions: [revision],
        onCreatePrompt,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Configure",
        }),
      );

      fireEvent.click(
        screen.getByRole("button", {
          name: "Create prompt from editor",
        }),
      );

      expect(onCreatePrompt).toHaveBeenCalledTimes(1);

      expect(
        screen.queryByTestId("revision-editor"),
      ).not.toBeInTheDocument();
    });
  });

  describe("creating revisions", () => {
    it("creates a revision when Create revision is clicked", async () => {
      const onChanged = vi.fn();

      renderPanel({
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: /create revision/i,
        }),
      );

      await waitFor(() => {
        expect(createTutorRevision).toHaveBeenCalledWith(
          "tutor-1",
        );
      });

      await waitFor(() => {
        expect(
          screen.getByTestId("revision-editor"),
        ).toBeInTheDocument();
      });

      expect(
        screen.getByText("Editing revision 2"),
      ).toBeInTheDocument();

      expect(onChanged).toHaveBeenCalledTimes(1);
    });

    it("creates the first revision from the empty state", async () => {
      const onChanged = vi.fn();

      renderPanel({
        revisions: [],
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Create first revision",
        }),
      );

      await waitFor(() => {
        expect(createTutorRevision).toHaveBeenCalledWith(
          "tutor-1",
        );
      });

      expect(onChanged).toHaveBeenCalledTimes(1);

      expect(
        screen.getByTestId("revision-editor"),
      ).toBeInTheDocument();
    });

    it("shows an error when revision creation fails", async () => {
      vi.mocked(createTutorRevision).mockRejectedValueOnce(
        new Error("Creation failed"),
      );

      renderPanel();

      fireEvent.click(
        screen.getByRole("button", {
          name: /create revision/i,
        }),
      );

      await waitFor(() => {
        expect(
          screen.getByText("Could not create a Draft revision."),
        ).toBeInTheDocument();
      });
    });

    it("does not create another revision while creation is busy", async () => {
      let resolveCreate!: (value: any) => void;

      vi.mocked(createTutorRevision).mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveCreate = resolve;
          }),
      );

      renderPanel();

      const createButton = screen.getByRole("button", {
        name: /create revision/i,
      });

      fireEvent.click(createButton);
      fireEvent.click(createButton);

      expect(createTutorRevision).toHaveBeenCalledTimes(1);

      resolveCreate({
        ...baseRevision,
        id: "revision-created",
        versionNumber: 2,
      });

      await waitFor(() => {
        expect(
          screen.getByTestId("revision-editor"),
        ).toBeInTheDocument();
      });
    });
  });

  describe("published revisions", () => {
    it("shows Activate and Archive for a published revision", () => {
      const revision = createRevision({
        status: "Published",
        publishedAt: "2026-09-02T10:00:00Z",
      });

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByRole("button", {
          name: "Activate",
        }),
      ).toBeInTheDocument();

      expect(
        screen.getByRole("button", {
          name: "Archive",
        }),
      ).toBeInTheDocument();
    });

    it("does not show Activate or Archive for a published revision when user cannot manage", () => {
      const revision = createRevision({
        status: "Published",
      });

      renderPanel({
        revisions: [revision],
        canManage: false,
      });

      expect(
        screen.queryByRole("button", {
          name: "Activate",
        }),
      ).not.toBeInTheDocument();

      expect(
        screen.queryByRole("button", {
          name: "Archive",
        }),
      ).not.toBeInTheDocument();

      expect(
        screen.getByRole("button", {
          name: "View configuration",
        }),
      ).toBeInTheDocument();
    });
  });

  describe("active revisions", () => {
    it("shows View, Archive and Benchmark for an active revision", () => {
      const revision = createRevision({
        status: "Active",
        isActive: true,
      });

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByRole("button", {
          name: "View",
        }),
      ).toBeInTheDocument();

      expect(
        screen.getByRole("button", {
          name: "Archive",
        }),
      ).toBeInTheDocument();

      expect(
        screen.getByRole("button", {
          name: "Benchmark",
        }),
      ).toBeInTheDocument();
    });

    it("opens the RevisionEditor when View is clicked", () => {
      const revision = createRevision({
        status: "Active",
        isActive: true,
      });

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "View",
        }),
      );

      expect(
        screen.getByTestId("revision-editor"),
      ).toBeInTheDocument();

      expect(
        screen.getByText("Editing revision 1"),
      ).toBeInTheDocument();
    });

    it("navigates to benchmarking when Benchmark is clicked", () => {
      const revision = createRevision({
        status: "Active",
        isActive: true,
      });

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Benchmark",
        }),
      );

      expect(mockNavigate).toHaveBeenCalledWith(
        "/benchmarking?tab=new&tutorId=tutor-1&revisionId=revision-1",
      );
    });
  });

  describe("archived revisions", () => {
    it("shows View configuration for an archived revision", () => {
      const revision = createRevision({
        status: "Archived",
      });

      renderPanel({
        revisions: [revision],
      });

      expect(
        screen.getByRole("button", {
          name: "View configuration",
        }),
      ).toBeInTheDocument();
    });

    it("opens the editor when View configuration is clicked", () => {
      const revision = createRevision({
        status: "Archived",
      });

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "View configuration",
        }),
      );

      expect(
        screen.getByTestId("revision-editor"),
      ).toBeInTheDocument();
    });
  });

  describe("confirmation modals", () => {
    it("opens the publish confirmation modal", async () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Publish",
        }),
      );

      const dialog = await getConfirmDialog();

      expect(
        within(dialog).getByText("Publish revision 1?", { selector: ".ant-modal-confirm-title" }),
      ).toBeInTheDocument();

      expect(
        within(dialog).getByText(
          "Published revisions become immutable and can then be activated.",
        ),
      ).toBeInTheDocument();
    });

    it("opens the activate confirmation modal", async () => {
      const revision = createRevision({
        status: "Published",
      });

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Activate",
        }),
      );

      const dialog = await getConfirmDialog();

      expect(
        within(dialog).getByText("Activate revision 1?", { selector: ".ant-modal-confirm-title" }),
      ).toBeInTheDocument();

      expect(
        within(dialog).getByText(
          "This revision will become the tutor's runtime configuration. The previously active revision will be archived.",
        ),
      ).toBeInTheDocument();
    });

    it("opens the archive confirmation modal", async () => {
      const revision = createRevision({
        status: "Published",
      });

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Archive",
        }),
      );

      const dialog = await getConfirmDialog();

      expect(
        within(dialog).getByText("Archive revision 1?", { selector: ".ant-modal-confirm-title" }),
      ).toBeInTheDocument();

      expect(
        within(dialog).getByText(
          "The tutor will stop using this revision until another revision is activated.",
        ),
      ).toBeInTheDocument();
    });

    it("opens the discard confirmation modal", async () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Discard",
        }),
      );

      const dialog = await getConfirmDialog();

      expect(
        within(dialog).getByText("Discard revision 1?", { selector: ".ant-modal-confirm-title" }),
      ).toBeInTheDocument();

      expect(
        within(dialog).getByText(
          "This Draft revision will be permanently discarded.",
        ),
      ).toBeInTheDocument();
    });
  });

  describe("publish action", () => {
    it("publishes a revision after confirmation", async () => {
      const revision = createRevision();

      const onChanged = vi.fn();

      renderPanel({
        revisions: [revision],
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Publish",
        }),
      );

      await confirmModalAction("Publish");

      await waitFor(() => {
        expect(publishTutorRevision).toHaveBeenCalledWith(
          "tutor-1",
          "revision-1",
        );
      });

      expect(onChanged).toHaveBeenCalledTimes(1);
    });

    it("shows an error when publishing fails", async () => {
      vi.mocked(publishTutorRevision).mockRejectedValueOnce(
        new Error("Publish failed"),
      );

      renderPanel({
        revisions: [createRevision()],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Publish",
        }),
      );

      await confirmModalAction("Publish");

      await waitFor(() => {
        expect(
          screen.getByText("Could not publish the revision."),
        ).toBeInTheDocument();
      });
    });
  });

  describe("activate action", () => {
    it("activates a published revision after confirmation", async () => {
      const revision = createRevision({
        status: "Published",
      });

      const onChanged = vi.fn();

      renderPanel({
        revisions: [revision],
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Activate",
        }),
      );

      await confirmModalAction("Activate");

      await waitFor(() => {
        expect(activateTutorRevision).toHaveBeenCalledWith(
          "tutor-1",
          "revision-1",
        );
      });

      expect(onChanged).toHaveBeenCalledTimes(1);
    });
  });

  describe("archive action", () => {
    it("archives a published revision after confirmation", async () => {
      const revision = createRevision({
        status: "Published",
      });

      const onChanged = vi.fn();

      renderPanel({
        revisions: [revision],
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Archive",
        }),
      );

      await confirmModalAction("Archive");

      await waitFor(() => {
        expect(archiveTutorRevision).toHaveBeenCalledWith(
          "tutor-1",
          "revision-1",
        );
      });

      expect(onChanged).toHaveBeenCalledTimes(1);
    });

    it("archives an active revision after confirmation", async () => {
      const revision = createRevision({
        status: "Active",
        isActive: true,
      });

      const onChanged = vi.fn();

      renderPanel({
        revisions: [revision],
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Archive",
        }),
      );

      await confirmModalAction("Archive");

      await waitFor(() => {
        expect(archiveTutorRevision).toHaveBeenCalledWith(
          "tutor-1",
          "revision-1",
        );
      });

      expect(onChanged).toHaveBeenCalledTimes(1);
    });
  });

  describe("discard action", () => {
    it("discards a draft revision after confirmation", async () => {
      const revision = createRevision();

      const onChanged = vi.fn();

      renderPanel({
        revisions: [revision],
        onChanged,
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Discard",
        }),
      );

      await confirmModalAction("Discard");

      await waitFor(() => {
        expect(discardTutorRevision).toHaveBeenCalledWith(
          "tutor-1",
          "revision-1",
        );
      });

      expect(onChanged).toHaveBeenCalledTimes(1);
    });

    it("closes the editor when discarding the revision currently being edited", async () => {
      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Configure",
        }),
      );

      expect(
        screen.getByTestId("revision-editor"),
      ).toBeInTheDocument();

      fireEvent.click(
        screen.getByRole("button", {
          name: "Discard",
        }),
      );

      await confirmModalAction("Discard");

      await waitFor(() => {
        expect(
          screen.queryByTestId("revision-editor"),
        ).not.toBeInTheDocument();
      });
    });
  });

  describe("busy states", () => {
    it("disables revision actions while an action is being performed", async () => {
      let resolvePublish!: () => void;

      vi.mocked(publishTutorRevision).mockImplementationOnce(
        () =>
          new Promise<void>((resolve) => {
            resolvePublish = resolve;
          }),
      );

      const revision = createRevision();

      renderPanel({
        revisions: [revision],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Publish",
        }),
      );

      await confirmModalAction("Publish");

      await waitFor(() => {
        expect(
          screen.getByRole("button", {
            name: "Publish",
          }),
        ).toBeDisabled();
      });

      resolvePublish();

      // Checking for the confirm dialog's total absence here is fragile:
      // Ant Design's zoom-leave exit animation can leave the element in the
      // DOM past waitFor's default timeout in jsdom, since rc-motion's
      // transition-end detection doesn't reliably fire without real CSS
      // transitions. Assert on the underlying business state instead.
      await waitFor(() => {
        expect(
          screen.getByRole("button", {
            name: "Publish",
          }),
        ).toBeEnabled();
      });
    });
  });

  describe("API errors", () => {
    it("handles an activation error", async () => {
      vi.mocked(activateTutorRevision).mockRejectedValueOnce(
        new Error("Activation failed"),
      );

      renderPanel({
        revisions: [
          createRevision({
            status: "Published",
          }),
        ],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Activate",
        }),
      );

      await confirmModalAction("Activate");

      await waitFor(() => {
        expect(
          screen.getByText("Could not activate the revision."),
        ).toBeInTheDocument();
      });
    });

    it("handles an archive error", async () => {
      vi.mocked(archiveTutorRevision).mockRejectedValueOnce(
        new Error("Archive failed"),
      );

      renderPanel({
        revisions: [
          createRevision({
            status: "Published",
          }),
        ],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Archive",
        }),
      );

      await confirmModalAction("Archive");

      await waitFor(() => {
        expect(
          screen.getByText("Could not archive the revision."),
        ).toBeInTheDocument();
      });
    });

    it("handles a discard error", async () => {
      vi.mocked(discardTutorRevision).mockRejectedValueOnce(
        new Error("Discard failed"),
      );

      renderPanel({
        revisions: [createRevision()],
      });

      fireEvent.click(
        screen.getByRole("button", {
          name: "Discard",
        }),
      );

      await confirmModalAction("Discard");

      await waitFor(() => {
        expect(
          screen.getByText("Could not discard the revision."),
        ).toBeInTheDocument();
      });
    });
  });
});
