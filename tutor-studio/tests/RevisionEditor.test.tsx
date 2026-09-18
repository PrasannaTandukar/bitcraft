import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "antd";
import RevisionEditor from "../src/features/editor/components/RevisionEditor.jsx";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock("../src/api/client.js", () => ({
  __esModule: true,
  getApiErrorDescription: vi.fn((_error: unknown, fallback: string) => fallback),
}));

vi.mock("../src/features/editor/hooks/tutorApi.js", () => ({
  __esModule: true,
  updateTutorRevision: vi.fn(),
}));

vi.mock("../src/features/editor/utils/tutorUtils.js", () => ({
  __esModule: true,
  deploymentTarget: vi.fn((value: number) => (Number(value) === 1 ? "Server" : "Local")),
  parseJsonObject: vi.fn((json: string | null | undefined) => {
    if (!json) return {};
    if (json === "INVALID_JSON") throw new Error("Unexpected token in JSON");
    if (json === "INVALID_JSON_NO_MESSAGE") throw new Error("");
    return JSON.parse(json);
  }),
  prettyJson: vi.fn((json: string | null | undefined) => {
    if (!json) return "";
    return JSON.stringify(JSON.parse(json), null, 2);
  }),
  revisionStatus: vi.fn((status: unknown) => status),
}));

import { getApiErrorDescription } from "../src/api/client.js";
import { updateTutorRevision } from "../src/features/editor/hooks/tutorApi.js";
import { deploymentTarget, parseJsonObject, prettyJson, revisionStatus } from "../src/features/editor/utils/tutorUtils.js";

const mockGetApiErrorDescription = vi.mocked(getApiErrorDescription);
const mockUpdateTutorRevision = vi.mocked(updateTutorRevision);
const mockDeploymentTarget = vi.mocked(deploymentTarget);
const mockParseJsonObject = vi.mocked(parseJsonObject);
const mockPrettyJson = vi.mocked(prettyJson);
const mockRevisionStatus = vi.mocked(revisionStatus);

// ---------------------------------------------------------------------------
// Fixtures / helpers
// ---------------------------------------------------------------------------

const baseRevision = {
  id: "rev1",
  versionNumber: 3,
  status: "Draft",
  promptVersionId: "p1",
  interactionModelId: "m1",
  interactionModelName: "GPT-4o",
  providerModelName: "gpt-4o",
  queryEmbeddingModelId: "e1",
  queryEmbeddingModelName: "Ada",
  queryEmbeddingProviderModelName: "text-embedding-ada-002",
  queryEmbeddingDimensions: 1536,
  deploymentTarget: 0,
  interactionParametersJson: JSON.stringify({ temperature: 0.7, top_p: 0.9, max_tokens: 512 }),
  deploymentConfigJson: JSON.stringify({ region: "us-east-1" }),
};

const basePrompts = [{ id: "p1", versionNumber: 1, name: "Default prompt" }];
const baseInteractionModels = [{ id: "m1", name: "GPT-4o", providerModelName: "gpt-4o" }];
const baseEmbeddingModels = [
  { id: "e1", name: "Ada", providerModelName: "text-embedding-ada-002", embeddingDimensions: 1536 },
];

function setup(overrides: Record<string, any> = {}) {
  const props = {
    tutorId: "tutor1",
    revision: baseRevision,
    prompts: basePrompts,
    interactionModels: baseInteractionModels,
    embeddingModels: baseEmbeddingModels,
    onClose: vi.fn(),
    onSaved: vi.fn(),
    onCreatePrompt: vi.fn(),
    ...overrides,
  };
  // App.useApp() needs an <App> ancestor for modal.confirm() to render as a
  // real, queryable Modal instead of falling back to antd's legacy static
  // Modal path.
  const utils = render(
    <App>
      <RevisionEditor {...props} />
    </App>
  );
  return { ...utils, props };
}

function getFormItemByLabel(labelText: string): HTMLElement {
  const label = screen.getByText(labelText, { selector: "label" });
  const formItem = label.closest(".ant-form-item") as HTMLElement;
  if (!formItem) throw new Error(`Form item not found for label: ${labelText}`);
  return formItem;
}

// The Drawer's header close icon (aria-label="Close") and the footer's
// plain <Button>Close</Button> both compute the same accessible name — same
// ambiguity hit with Modal in the DocumentUploadDialog suite. Scope to the
// footer container to get the actual footer button.
function getFooterCloseButton(): HTMLElement {
  const footer = document.querySelector(".ant-drawer-footer") as HTMLElement;
  return within(footer).getByRole("button", { name: "Close" });
}

// Modal.confirm() renders its title text twice: once in the dialog's
// aria-labelledby target (.ant-modal-title) and once in a separate visual
// element (.ant-modal-confirm-title). A single findByText/getByText call is
// ambiguous by design here; assert against the full set instead.
async function findConfirmTitle(text: string): Promise<HTMLElement[]> {
  const matches = await screen.findAllByText(text);
  expect(matches.length).toBeGreaterThan(0);
  return matches;
}

// Same rc-select quirk as earlier suites: only the real, visible option row
// (inside the portaled .ant-select-dropdown) carries a `title` attribute.
async function selectOption(user: ReturnType<typeof userEvent.setup>, fieldLabel: string, optionText: string) {
  await user.click(screen.getByLabelText(fieldLabel));
  const dropdown = await waitFor(() => {
    const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
    if (!el) throw new Error("Select dropdown is not open yet");
    return el as HTMLElement;
  });
  const option = within(dropdown).getByTitle(optionText);
  await user.click(option);
}

async function clearSelect(user: ReturnType<typeof userEvent.setup>, fieldLabel: string) {
  const formItem = getFormItemByLabel(fieldLabel);
  const clearIcon = within(formItem).getByLabelText("close-circle");
  await user.click(clearIcon);
}

async function openAdvancedPanel(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByText("Advanced JSON configuration"));
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

beforeEach(() => {
  vi.clearAllMocks();
  mockGetApiErrorDescription.mockImplementation((_error: unknown, fallback: string) => fallback);
  mockDeploymentTarget.mockImplementation((value: number) => (Number(value) === 1 ? "Server" : "Local"));
  mockParseJsonObject.mockImplementation((json: string | null | undefined) => {
    if (!json) return {};
    if (json === "INVALID_JSON") throw new Error("Unexpected token in JSON");
    if (json === "INVALID_JSON_NO_MESSAGE") throw new Error("");
    return JSON.parse(json);
  });
  mockPrettyJson.mockImplementation((json: string | null | undefined) => {
    if (!json) return "";
    return JSON.stringify(JSON.parse(json), null, 2);
  });
  mockRevisionStatus.mockImplementation((status: unknown) => status);
});

// ---------------------------------------------------------------------------
// Rendering: editable vs read-only
// ---------------------------------------------------------------------------

describe("RevisionEditor - editable vs read-only", () => {
  it("shows a Configure title and Save draft button for a Draft revision", () => {
    setup({ revision: { ...baseRevision, status: "Draft", versionNumber: 3 } });
    expect(screen.getByText("Configure revision 3")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Save draft" })).toBeInTheDocument();
    expect(screen.queryByText(/revisions are immutable/i)).not.toBeInTheDocument();
  });

  it("shows a View title, immutability alert, and no Save button for a non-Draft revision", () => {
    setup({ revision: { ...baseRevision, status: "Published", versionNumber: 3 } });
    expect(screen.getByText("View revision 3")).toBeInTheDocument();
    expect(screen.getByText("Published revisions are immutable")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /save draft/i })).not.toBeInTheDocument();
  });

  it("disables form fields when the revision is not editable", () => {
    setup({ revision: { ...baseRevision, status: "Published" } });
    expect(screen.getByLabelText("Temperature")).toBeDisabled();
  });

  it("shows the 'no interaction model available' warning when there are no options", () => {
    setup({
      interactionModels: [],
      revision: { ...baseRevision, interactionModelId: undefined },
    });
    expect(
      screen.getByText("No enabled interaction model is available. An administrator must configure one in Model Catalog.")
    ).toBeInTheDocument();
  });

  it("does not show the interaction model warning when at least one option exists", () => {
    setup();
    expect(screen.queryByText(/No enabled interaction model is available/i)).not.toBeInTheDocument();
  });

  it("shows a 'Create a prompt version' link when there are no prompts and the revision is editable", () => {
    setup({ prompts: [], revision: { ...baseRevision, status: "Draft" } });
    expect(screen.getByRole("button", { name: "Create a prompt version" })).toBeInTheDocument();
  });

  it("hides the 'Create a prompt version' link when prompts exist", () => {
    setup({ prompts: basePrompts });
    expect(screen.queryByRole("button", { name: "Create a prompt version" })).not.toBeInTheDocument();
  });

  it("hides the 'Create a prompt version' link when not editable, even with no prompts", () => {
    setup({ prompts: [], revision: { ...baseRevision, status: "Published" } });
    expect(screen.queryByRole("button", { name: "Create a prompt version" })).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Close / dirty-confirmation flow
// ---------------------------------------------------------------------------

describe("RevisionEditor - close and dirty confirmation", () => {
  it("calls onClose directly when closing while not dirty", async () => {
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(getFooterCloseButton());
    expect(props.onClose).toHaveBeenCalledTimes(1);
  });

  it("asks for confirmation before closing once the form is dirty, and closes on Discard", async () => {
    const user = userEvent.setup();
    const { props } = setup();

    await user.clear(screen.getByLabelText("Temperature"));
    await user.type(screen.getByLabelText("Temperature"), "1");

    await user.click(getFooterCloseButton());
    await findConfirmTitle("Discard unsaved changes?");
    expect(props.onClose).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(props.onClose).toHaveBeenCalledTimes(1);
  });

  it("does not close when the discard confirmation is cancelled", async () => {
    const user = userEvent.setup();
    const { props } = setup();

    await user.clear(screen.getByLabelText("Temperature"));
    await user.type(screen.getByLabelText("Temperature"), "1");
    await user.click(getFooterCloseButton());

    const cancelButton = await screen.findByRole("button", { name: "Cancel" });
    await user.click(cancelButton);
    expect(props.onClose).not.toHaveBeenCalled();
  });

  it("calls onCreatePrompt directly when not dirty", async () => {
    const user = userEvent.setup();
    const { props } = setup({ prompts: [] });
    await user.click(screen.getByRole("button", { name: "Create a prompt version" }));
    expect(props.onCreatePrompt).toHaveBeenCalledTimes(1);
  });

  it("confirms before creating a prompt version when dirty, and proceeds on Leave editor", async () => {
    const user = userEvent.setup();
    const { props } = setup({ prompts: [] });

    await user.clear(screen.getByLabelText("Temperature"));
    await user.type(screen.getByLabelText("Temperature"), "1");
    await user.click(screen.getByRole("button", { name: "Create a prompt version" }));

    await findConfirmTitle("Leave the revision editor?");
    expect(props.onCreatePrompt).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Leave editor" }));
    expect(props.onCreatePrompt).toHaveBeenCalledTimes(1);
  });

  it("registers a beforeunload warning once dirty, and removes it after a successful save", async () => {
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const addSpy = vi.spyOn(window, "addEventListener");
    const removeSpy = vi.spyOn(window, "removeEventListener");
    const user = userEvent.setup();
    setup();

    expect(addSpy).not.toHaveBeenCalledWith("beforeunload", expect.any(Function));

    await user.clear(screen.getByLabelText("Temperature"));
    await user.type(screen.getByLabelText("Temperature"), "1");

    await waitFor(() => expect(addSpy).toHaveBeenCalledWith("beforeunload", expect.any(Function)));

    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => expect(removeSpy).toHaveBeenCalledWith("beforeunload", expect.any(Function)));
  });
});

// ---------------------------------------------------------------------------
// Save payload composition
// ---------------------------------------------------------------------------

describe("RevisionEditor - save payload", () => {
  it("submits the expected payload unchanged from initial values, once the Advanced JSON panel has been opened", async () => {
    // Ant Design's Collapse does not mount a panel's children into the Form
    // until it has been expanded at least once (no forceRender on this
    // item), so the "parametersJson"/"deploymentJson" textareas are not
    // registered with form state unless this panel has been opened. This is
    // the intended full round-trip scenario: everything visible, nothing
    // touched.
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();

    await openAdvancedPanel(user);
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => {
      expect(mockUpdateTutorRevision).toHaveBeenCalledWith("tutor1", "rev1", {
        promptVersionId: "p1",
        interactionModelId: "m1",
        queryEmbeddingModelId: "e1",
        interactionParametersJson: JSON.stringify({ temperature: 0.7, top_p: 0.9, max_tokens: 512 }),
        deploymentTarget: 0,
        deploymentConfigJson: JSON.stringify({ region: "us-east-1" }),
      });
    });
  });

  it("QUIRK: silently drops deploymentConfigJson to null when saving without ever opening the Advanced JSON panel", async () => {
    // This documents real, observed behavior rather than an assumption: the
    // "Deployment configuration JSON" field lives inside a collapsed-by-
    // default Collapse panel. Because that panel has no forceRender, its
    // Form.Item is not mounted (and so never registers a value) until the
    // user expands it at least once. interactionParametersJson does NOT
    // show the same symptom, purely because its three keys get fully
    // reconstructed from the separate, always-mounted Temperature/Top P/
    // Maximum tokens inputs regardless of the raw textarea's state — that
    // reconstruction has no equivalent for deployment config. A user who
    // saves a Draft without expanding "Advanced JSON configuration" will
    // have their existing deployment JSON silently wiped to null. Worth
    // flagging upstream as likely unintended; pinned down here so a future
    // fix doesn't regress unnoticed.
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => {
      expect(mockUpdateTutorRevision).toHaveBeenCalledWith(
        "tutor1",
        "rev1",
        expect.objectContaining({ deploymentConfigJson: null })
      );
    });
  });

  it("sends null for cleared optional select fields", async () => {
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();

    await clearSelect(user, "Prompt version");
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => {
      expect(mockUpdateTutorRevision).toHaveBeenCalledWith(
        "tutor1",
        "rev1",
        expect.objectContaining({ promptVersionId: null })
      );
    });
  });

  it("omits a numeric parameter from the JSON payload when its field is cleared", async () => {
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();

    await user.clear(screen.getByLabelText("Temperature"));
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => {
      expect(mockUpdateTutorRevision).toHaveBeenCalledWith(
        "tutor1",
        "rev1",
        expect.objectContaining({
          interactionParametersJson: JSON.stringify({ top_p: 0.9, max_tokens: 512 }),
        })
      );
    });
  });

  it("sends the numeric deployment target selected from the dropdown", async () => {
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();

    await selectOption(user, "Deployment", "Server");
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => {
      expect(mockUpdateTutorRevision).toHaveBeenCalledWith(
        "tutor1",
        "rev1",
        expect.objectContaining({ deploymentTarget: 1 })
      );
    });
  });

  it("sends null for interactionParametersJson and deploymentConfigJson when both end up empty", async () => {
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup({
      revision: {
        ...baseRevision,
        interactionParametersJson: null,
        deploymentConfigJson: null,
      },
    });

    await user.clear(screen.getByLabelText("Temperature"));
    await user.clear(screen.getByLabelText("Top P"));
    await user.clear(screen.getByLabelText("Maximum tokens"));
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => {
      expect(mockUpdateTutorRevision).toHaveBeenCalledWith(
        "tutor1",
        "rev1",
        expect.objectContaining({
          interactionParametersJson: null,
          deploymentConfigJson: null,
        })
      );
    });
  });

  it("calls onSaved after a successful save", async () => {
    mockUpdateTutorRevision.mockResolvedValue(undefined);
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(screen.getByRole("button", { name: "Save draft" }));
    await waitFor(() => expect(props.onSaved).toHaveBeenCalledTimes(1));
  });
});

// ---------------------------------------------------------------------------
// Advanced JSON validation
// ---------------------------------------------------------------------------

describe("RevisionEditor - advanced JSON validation", () => {
  it("shows the parser's own error message when the parameters JSON is invalid", async () => {
    const user = userEvent.setup();
    setup();

    await openAdvancedPanel(user);
    const textarea = screen.getByLabelText("Interaction parameters JSON");
    await user.clear(textarea);
    await user.type(textarea, "INVALID_JSON");
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    expect(await screen.findByText("Unexpected token in JSON")).toBeInTheDocument();
    expect(mockUpdateTutorRevision).not.toHaveBeenCalled();
  });

  it("falls back to a generic message when the parse error has no message", async () => {
    const user = userEvent.setup();
    setup();

    await openAdvancedPanel(user);
    const textarea = screen.getByLabelText("Interaction parameters JSON");
    await user.clear(textarea);
    await user.type(textarea, "INVALID_JSON_NO_MESSAGE");
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    expect(await screen.findByText("Advanced configuration must be valid JSON objects.")).toBeInTheDocument();
    expect(mockUpdateTutorRevision).not.toHaveBeenCalled();
  });

  it("does not get stuck in a loading state after a JSON validation failure", async () => {
    const user = userEvent.setup();
    setup();

    await openAdvancedPanel(user);
    const textarea = screen.getByLabelText("Interaction parameters JSON");
    await user.clear(textarea);
    await user.type(textarea, "INVALID_JSON");
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await screen.findByText("Unexpected token in JSON");
    expect(screen.getByRole("button", { name: "Save draft" }).querySelector(".anticon-loading")).toBeFalsy();
  });
});

// ---------------------------------------------------------------------------
// Busy state / save failure
// ---------------------------------------------------------------------------

describe("RevisionEditor - busy state and failures", () => {
  it("shows a loading Save button and disables Close while saving", async () => {
    const deferred = createDeferred<void>();
    mockUpdateTutorRevision.mockReturnValue(deferred.promise as any);
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /save draft/i }).querySelector(".anticon-loading")).toBeTruthy();
    });
    expect(getFooterCloseButton()).toBeDisabled();

    deferred.resolve();
    await waitFor(() => expect(getFooterCloseButton()).toBeEnabled());
  });

  it("shows the API error fallback and does not call onSaved when the save request fails", async () => {
    mockUpdateTutorRevision.mockRejectedValue(new Error("network down"));
    const user = userEvent.setup();
    const { props } = setup();

    await user.click(screen.getByRole("button", { name: "Save draft" }));

    expect(await screen.findByText("Could not save the revision configuration.")).toBeInTheDocument();
    expect(props.onSaved).not.toHaveBeenCalled();
  });

  it("re-enables Save (not stuck loading) after a failed save", async () => {
    mockUpdateTutorRevision.mockRejectedValue(new Error("network down"));
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole("button", { name: "Save draft" }));
    await screen.findByText("Could not save the revision configuration.");

    expect(screen.getByRole("button", { name: "Save draft" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Save draft" }).querySelector(".anticon-loading")).toBeFalsy();
  });
});
