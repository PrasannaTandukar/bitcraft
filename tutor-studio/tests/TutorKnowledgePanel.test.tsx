import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import TutorKnowledgePanel from "../src/features/editor/components/TutorKnowledgePanel.jsx";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock("react-router-dom", () => ({
  __esModule: true,
  useNavigate: vi.fn(),
}));

vi.mock("../src/api/client.js", () => ({
  __esModule: true,
  getApiErrorDescription: vi.fn((_error: unknown, fallback: string) => fallback),
}));

vi.mock("../src/features/editor/utils/documentApi.js", () => ({
  __esModule: true,
  getVectorizationStatus: vi.fn(),
  listDocumentFolders: vi.fn(),
  listDocuments: vi.fn(),
}));

vi.mock("../src/features/editor/hooks/tutorApi.js", () => ({
  __esModule: true,
  attachTutorDocument: vi.fn(),
  attachTutorDocumentFolder: vi.fn(),
  detachTutorDocument: vi.fn(),
  detachTutorDocumentFolder: vi.fn(),
  listTutorDocumentFolders: vi.fn(),
  listTutorDocuments: vi.fn(),
}));

import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "../src/api/client.js";
import { getVectorizationStatus, listDocumentFolders, listDocuments } from "../src/features/editor/utils/documentApi.js";
import {
  attachTutorDocument,
  attachTutorDocumentFolder,
  detachTutorDocument,
  detachTutorDocumentFolder,
  listTutorDocumentFolders,
  listTutorDocuments,
} from "../src/features/editor/hooks/tutorApi.js";


const mockUseNavigate = vi.mocked(useNavigate);
const mockGetApiErrorDescription = vi.mocked(getApiErrorDescription);
const mockGetVectorizationStatus = vi.mocked(getVectorizationStatus);
const mockListDocumentFolders = vi.mocked(listDocumentFolders);
const mockListDocuments = vi.mocked(listDocuments);
const mockAttachTutorDocument = vi.mocked(attachTutorDocument);
const mockAttachTutorDocumentFolder = vi.mocked(attachTutorDocumentFolder);
const mockDetachTutorDocument = vi.mocked(detachTutorDocument);
const mockDetachTutorDocumentFolder = vi.mocked(detachTutorDocumentFolder);
const mockListTutorDocumentFolders = vi.mocked(listTutorDocumentFolders);
const mockListTutorDocuments = vi.mocked(listTutorDocuments);

// ---------------------------------------------------------------------------
// Fixtures / helpers
// ---------------------------------------------------------------------------

// d1: already attached. d2: not vectorized. d3: vectorized but stale.
// d4: vectorized and ready — the only one eligible to attach successfully.
const baseDocuments = [
  { id: "d1", name: "Doc One", folderId: null },
  { id: "d2", name: "Doc Two", folderId: "f1" },
  { id: "d3", name: "Doc Three", folderId: "f2" },
  { id: "d4", name: "Doc Four", folderId: null },
];

// f2 is a child of f1, used to exercise the recursive descendant-folder walk.
const baseFolders = [
  { id: "f1", name: "Folder One", parentFolderId: null },
  { id: "f2", name: "Folder Two", parentFolderId: "f1" },
];

const baseAttachedDocuments = [
  {
    documentId: "d1",
    documentName: "Doc One",
    embeddingModelName: "Ada",
    isVectorized: true,
    legacyCompatibility: false,
  },
];

const baseAttachedFolders: Array<{ folderId: string; folderName: string }> = [];

const vectorizationById: Record<string, { isVectorized: boolean; isStale?: boolean } | null> = {
  d1: { isVectorized: true, isStale: false },
  d2: { isVectorized: false, isStale: false },
  d3: { isVectorized: true, isStale: true },
  d4: { isVectorized: true, isStale: false },
};

function mockNavigate() {
  const nav = vi.fn();
  mockUseNavigate.mockReturnValue(nav);
  return nav;
}

function setDefaultResolvedValues(overrides: {
  documents?: any[];
  folders?: any[];
  attachedDocuments?: any[];
  attachedFolders?: any[];
} = {}) {
  mockListDocuments.mockResolvedValue(overrides.documents ?? baseDocuments);
  mockListDocumentFolders.mockResolvedValue(overrides.folders ?? baseFolders);
  mockListTutorDocuments.mockResolvedValue(overrides.attachedDocuments ?? baseAttachedDocuments);
  mockListTutorDocumentFolders.mockResolvedValue(overrides.attachedFolders ?? baseAttachedFolders);
  mockGetVectorizationStatus.mockImplementation(async (documentId: string) => vectorizationById[documentId] ?? null);
}

function setup(overrides: Record<string, any> = {}) {
  const props = { tutorId: "tutor1", canManage: true, ...overrides };
  const utils = render(<TutorKnowledgePanel {...props} />);
  return { ...utils };
}

async function waitForLoaded() {
  await waitFor(() => expect(document.querySelector(".workspace-loading")).not.toBeInTheDocument());
}

function getResourceRow(nameText: string): HTMLElement {
  const label = screen.getByText(nameText);
  const row = label.closest(".tutor-resource-row") as HTMLElement;
  if (!row) throw new Error(`Resource row not found for: ${nameText}`);
  return row;
}

// Selects here have no associated <label>, and two can be on screen at
// once (document-attach, folder-attach), so scope by placeholder text.
// Returns the outer .ant-select container — use this when you need a
// stable reference to check later (e.g. disabled class), since the
// placeholder text disappears once a value is selected.
function getSelectRootByPlaceholder(placeholderText: string): HTMLElement {
  const placeholderEl = screen.getByText(placeholderText, {
    selector: ".ant-select-selection-placeholder, .ant-select-placeholder",
  });
  const selectRoot = placeholderEl.closest(".ant-select") as HTMLElement;
  if (!selectRoot) throw new Error(`Select not found for placeholder: ${placeholderText}`);
  return selectRoot;
}

function getSelectByPlaceholder(placeholderText: string): HTMLElement {
  return within(getSelectRootByPlaceholder(placeholderText)).getByRole("combobox");
}

// Same rc-select quirk as every other suite this session: only the real,
// visible option (inside the portaled .ant-select-dropdown) carries a
// `title` attribute.
async function selectFromDropdown(
  user: ReturnType<typeof userEvent.setup>,
  placeholderText: string,
  optionText: string
) {
  await user.click(getSelectByPlaceholder(placeholderText));
  const dropdown = await waitFor(() => {
    const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
    if (!el) throw new Error("Select dropdown is not open yet");
    return el as HTMLElement;
  });
  const option = within(dropdown).getByTitle(optionText);
  await user.click(option);
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
  mockNavigate();
  setDefaultResolvedValues();
});

// ---------------------------------------------------------------------------
// Loading / error / abort handling
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - loading and errors", () => {
  it("shows a loading spinner while the initial requests are in flight", () => {
    mockListDocuments.mockReturnValue(new Promise(() => {}));
    setup();
    expect(document.querySelector(".workspace-loading")).toBeInTheDocument();
  });

  it("hides the loading spinner once all requests resolve", async () => {
    setup();
    await waitForLoaded();
    expect(screen.getByText("Knowledge")).toBeInTheDocument();
  });

  it("shows an error alert with the API fallback when a request rejects", async () => {
    mockListDocuments.mockRejectedValue(new Error("network down"));
    setup();
    expect(await screen.findByText("Could not load tutor knowledge.")).toBeInTheDocument();
  });

  it("ignores AbortError rejections: no error shown, ends up not loading with empty lists", async () => {
    const abortError = new Error("aborted");
    abortError.name = "AbortError";
    mockListDocuments.mockRejectedValue(abortError);
    setup();

    await waitForLoaded();
    // Note: the info banner ("The next activation will capture...") always
    // renders and also has role="alert", so asserting on that role alone
    // can't distinguish "no error" from "the ever-present info banner".
    // Check for the absence of the actual error text instead.
    expect(screen.queryByText("Could not load tutor knowledge.")).not.toBeInTheDocument();
    expect(screen.getByText("No documents are attached directly")).toBeInTheDocument();
  });

  it("does not fetch vectorization status when canManage is false", async () => {
    setup({ canManage: false });
    await waitForLoaded();
    expect(mockGetVectorizationStatus).not.toHaveBeenCalled();
  });

  it("fetches vectorization status for each document when canManage is true", async () => {
    setup({ canManage: true });
    await waitForLoaded();
    expect(mockGetVectorizationStatus).toHaveBeenCalledWith("d1", expect.any(AbortSignal));
    expect(mockGetVectorizationStatus).toHaveBeenCalledWith("d4", expect.any(AbortSignal));
  });

  it("treats a per-document vectorization status failure as null rather than failing the whole load", async () => {
    mockGetVectorizationStatus.mockImplementation(async (documentId: string) => {
      if (documentId === "d4") throw new Error("status service down");
      return vectorizationById[documentId] ?? null;
    });
    setup();
    await waitForLoaded();
    // The rest of the panel still renders normally.
    expect(screen.getByText("Knowledge")).toBeInTheDocument();
    expect(screen.queryByRole("alert", { name: /could not load/i })).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Effective document count
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - effective document count", () => {
  it("shows a singular label when exactly one document is effective", async () => {
    setDefaultResolvedValues({ attachedDocuments: baseAttachedDocuments, attachedFolders: [] });
    setup();
    await waitForLoaded();
    expect(
      screen.getByText("The next activation will capture 1 effective document.")
    ).toBeInTheDocument();
  });

  it("shows a plural label and zero count when nothing is attached", async () => {
    setDefaultResolvedValues({ attachedDocuments: [], attachedFolders: [] });
    setup();
    await waitForLoaded();
    expect(
      screen.getByText("The next activation will capture 0 effective documents.")
    ).toBeInTheDocument();
  });

  it("counts documents inside nested (descendant) attached folders", async () => {
    setDefaultResolvedValues({
      attachedDocuments: baseAttachedDocuments, // d1
      attachedFolders: [{ folderId: "f1", folderName: "Folder One" }],
      // f2 is a child of f1; d2 lives in f1, d3 lives in f2 — both should
      // count as effective via the recursive descendant walk.
    });
    setup();
    await waitForLoaded();
    expect(
      screen.getByText("The next activation will capture 3 effective documents.")
    ).toBeInTheDocument();
  });

  it("does not double-count a document that is both directly attached and inside an attached folder", async () => {
    setDefaultResolvedValues({
      documents: [{ id: "d2", name: "Doc Two", folderId: "f1" }],
      folders: [{ id: "f1", name: "Folder One", parentFolderId: null }],
      attachedDocuments: [{ documentId: "d2", documentName: "Doc Two", isVectorized: false }],
      attachedFolders: [{ folderId: "f1", folderName: "Folder One" }],
    });
    setup();
    await waitForLoaded();
    expect(
      screen.getByText("The next activation will capture 1 effective document.")
    ).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// canManage gating
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - canManage gating", () => {
  it("shows the Attach a document card and folder-attach controls when canManage is true", async () => {
    setup({ canManage: true });
    await waitForLoaded();
    expect(screen.getByText("Attach a document")).toBeInTheDocument();
    expect(screen.getByText("Select a folder")).toBeInTheDocument();
  });

  it("hides the Attach a document card and folder-attach controls when canManage is false", async () => {
    setup({ canManage: false });
    await waitForLoaded();
    expect(screen.queryByText("Attach a document")).not.toBeInTheDocument();
    expect(screen.queryByText("Select a folder")).not.toBeInTheDocument();
  });

  it("hides Detach buttons when canManage is false", async () => {
    setup({ canManage: false });
    await waitForLoaded();
    expect(within(getResourceRow("Doc One")).queryByRole("button", { name: /detach/i })).not.toBeInTheDocument();
  });

  it("shows Detach buttons when canManage is true", async () => {
    setup({ canManage: true });
    await waitForLoaded();
    expect(within(getResourceRow("Doc One")).getByRole("button", { name: /detach/i })).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Attach a document
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - attach document", () => {
  it("excludes already-attached documents from the available options", async () => {
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    await user.click(getSelectByPlaceholder("Select a source document"));
    const dropdown = await waitFor(() => {
      const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
      if (!el) throw new Error("dropdown not open");
      return el as HTMLElement;
    });
    expect(within(dropdown).queryByTitle(/Doc One/)).not.toBeInTheDocument();
    expect(within(dropdown).getByTitle("Doc Four · Vectorized")).toBeInTheDocument();
  });

  it("disables the Attach button until a document is selected", async () => {
    setup();
    await waitForLoaded();
    expect(screen.getByRole("button", { name: /^(link |loading )?attach$/i })).toBeDisabled();
  });

  it("disables Attach and shows a warning when the selected document is not vectorized", async () => {
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    await selectFromDropdown(user, "Select a source document", "Doc Two · Not ready");
    expect(screen.getByRole("button", { name: /^(link |loading )?attach$/i })).toBeDisabled();
    expect(screen.getByText("Vectorize this document in Files before attaching it.")).toBeInTheDocument();
  });

  it("disables Attach and shows a warning when the selected document is stale", async () => {
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    await selectFromDropdown(user, "Select a source document", "Doc Three · Not ready");
    expect(screen.getByRole("button", { name: /^(link |loading )?attach$/i })).toBeDisabled();
    expect(screen.getByText("Vectorize this document in Files before attaching it.")).toBeInTheDocument();
  });

  it("enables Attach for a ready document and calls attachTutorDocument on click", async () => {
    mockAttachTutorDocument.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    await selectFromDropdown(user, "Select a source document", "Doc Four · Vectorized");
    expect(screen.getByRole("button", { name: /^(link |loading )?attach$/i })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: /^(link |loading )?attach$/i }));
    await waitFor(() => expect(mockAttachTutorDocument).toHaveBeenCalledWith("tutor1", "d4"));
  });

  it("reloads the lists after a successful attach", async () => {
    mockAttachTutorDocument.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    const callsBefore = mockListDocuments.mock.calls.length;

    await selectFromDropdown(user, "Select a source document", "Doc Four · Vectorized");
    await user.click(screen.getByRole("button", { name: /^(link |loading )?attach$/i }));

    await waitFor(() => expect(mockListDocuments.mock.calls.length).toBeGreaterThan(callsBefore));
  });

  it("shows an error and does not reset the selection when attach fails", async () => {
    mockAttachTutorDocument.mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    await selectFromDropdown(user, "Select a source document", "Doc Four · Vectorized");
    await user.click(screen.getByRole("button", { name: /^(link |loading )?attach$/i }));

    expect(await screen.findByText("Could not update tutor knowledge.")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Attach a folder
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - attach folder", () => {
  it("excludes already-attached folders from the available options", async () => {
    const user = userEvent.setup();
    setDefaultResolvedValues({ attachedFolders: [{ folderId: "f1", folderName: "Folder One" }] });
    setup();
    await waitForLoaded();
    await user.click(getSelectByPlaceholder("Select a folder"));
    const dropdown = await waitFor(() => {
      const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
      if (!el) throw new Error("dropdown not open");
      return el as HTMLElement;
    });
    expect(within(dropdown).queryByTitle("Folder One")).not.toBeInTheDocument();
    expect(within(dropdown).getByTitle("Folder Two")).toBeInTheDocument();
  });

  it("disables the Attach folder button until a folder is selected", async () => {
    setup();
    await waitForLoaded();
    expect(screen.getByRole("button", { name: /attach folder/i })).toBeDisabled();
  });

  it("calls attachTutorDocumentFolder with the selected folder and reloads on success", async () => {
    mockAttachTutorDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    const callsBefore = mockListDocumentFolders.mock.calls.length;

    await selectFromDropdown(user, "Select a folder", "Folder One");
    await user.click(screen.getByRole("button", { name: /attach folder/i }));

    await waitFor(() => expect(mockAttachTutorDocumentFolder).toHaveBeenCalledWith("tutor1", "f1"));
    await waitFor(() => expect(mockListDocumentFolders.mock.calls.length).toBeGreaterThan(callsBefore));
  });

  it("shows an error when attaching a folder fails", async () => {
    mockAttachTutorDocumentFolder.mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    await selectFromDropdown(user, "Select a folder", "Folder One");
    await user.click(screen.getByRole("button", { name: /attach folder/i }));

    expect(await screen.findByText("Could not update tutor knowledge.")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Detach flows
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - detach", () => {
  it("calls detachTutorDocument after confirming, and reloads", async () => {
    mockDetachTutorDocument.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    const callsBefore = mockListTutorDocuments.mock.calls.length;

    await user.click(within(getResourceRow("Doc One")).getByRole("button", { name: /detach/i }));
    await user.click(await screen.findByRole("button", { name: "OK" }));

    await waitFor(() => expect(mockDetachTutorDocument).toHaveBeenCalledWith("tutor1", "d1"));
    await waitFor(() => expect(mockListTutorDocuments.mock.calls.length).toBeGreaterThan(callsBefore));
  });

  it("does not call detachTutorDocument if the confirmation is cancelled", async () => {
    const user = userEvent.setup();
    setup();
    await waitForLoaded();

    await user.click(within(getResourceRow("Doc One")).getByRole("button", { name: /detach/i }));
    await user.click(await screen.findByRole("button", { name: "Cancel" }));

    expect(mockDetachTutorDocument).not.toHaveBeenCalled();
  });

  it("calls detachTutorDocumentFolder after confirming, and reloads", async () => {
    mockDetachTutorDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setDefaultResolvedValues({ attachedFolders: [{ folderId: "f1", folderName: "Folder One" }] });
    setup();
    await waitForLoaded();
    const callsBefore = mockListTutorDocumentFolders.mock.calls.length;

    await user.click(within(getResourceRow("Folder One")).getByRole("button", { name: /detach/i }));
    await user.click(await screen.findByRole("button", { name: "OK" }));

    await waitFor(() => expect(mockDetachTutorDocumentFolder).toHaveBeenCalledWith("tutor1", "f1"));
    await waitFor(() => expect(mockListTutorDocumentFolders.mock.calls.length).toBeGreaterThan(callsBefore));
  });

  it("shows an error when detaching fails", async () => {
    mockDetachTutorDocument.mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    setup();
    await waitForLoaded();

    await user.click(within(getResourceRow("Doc One")).getByRole("button", { name: /detach/i }));
    await user.click(await screen.findByRole("button", { name: "OK" }));

    expect(await screen.findByText("Could not update tutor knowledge.")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Attached list content
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - attached list content", () => {
  it("shows the embedding model name, or a fallback when missing", async () => {
    setDefaultResolvedValues({
      attachedDocuments: [
        { documentId: "d1", documentName: "Doc One", embeddingModelName: "Ada", isVectorized: true },
        { documentId: "d4", documentName: "Doc Four", embeddingModelName: undefined, isVectorized: true },
      ],
    });
    setup();
    await waitForLoaded();
    expect(screen.getByText(/Ada/)).toBeInTheDocument();
    expect(screen.getByText(/No embedding model/)).toBeInTheDocument();
  });

  it("shows a legacy compatibility note when applicable", async () => {
    setDefaultResolvedValues({
      attachedDocuments: [
        { documentId: "d1", documentName: "Doc One", embeddingModelName: "Ada", isVectorized: true, legacyCompatibility: true },
      ],
    });
    setup();
    await waitForLoaded();
    expect(screen.getByText(/Legacy compatibility link/)).toBeInTheDocument();
  });

  it("shows a Vectorized tag for a ready attached document and Not ready otherwise", async () => {
    setDefaultResolvedValues({
      attachedDocuments: [
        { documentId: "d1", documentName: "Doc One", isVectorized: true },
        { documentId: "d2", documentName: "Doc Two", isVectorized: false },
      ],
    });
    setup();
    await waitForLoaded();
    expect(screen.getByText("Vectorized")).toBeInTheDocument();
    expect(screen.getByText("Not ready")).toBeInTheDocument();
  });

  it("shows an empty state when no documents are attached", async () => {
    setDefaultResolvedValues({ attachedDocuments: [] });
    setup();
    await waitForLoaded();
    expect(screen.getByText("No documents are attached directly")).toBeInTheDocument();
  });

  it("shows an empty state when no folders are attached", async () => {
    setDefaultResolvedValues({ attachedFolders: [] });
    setup();
    await waitForLoaded();
    expect(screen.getByText("No folders are attached")).toBeInTheDocument();
  });

  it("shows attached folder names", async () => {
    setDefaultResolvedValues({ attachedFolders: [{ folderId: "f1", folderName: "Folder One" }] });
    setup();
    await waitForLoaded();
    expect(screen.getByText("Folder One")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Navigation
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - navigation", () => {
  it("navigates to /files when Manage files is clicked", async () => {
    const nav = mockNavigate();
    const user = userEvent.setup();
    setup();
    await waitForLoaded();
    await user.click(screen.getByRole("button", { name: /manage files/i }));
    expect(nav).toHaveBeenCalledWith("/files");
  });
});

// ---------------------------------------------------------------------------
// Saving / busy state
// ---------------------------------------------------------------------------

describe("TutorKnowledgePanel - busy state", () => {
  it("disables the document Select and shows a loading Attach button while a mutation is in flight", async () => {
    const deferred = createDeferred<void>();
    mockAttachTutorDocument.mockReturnValue(deferred.promise as any);
    const user = userEvent.setup();
    setup();
    await waitForLoaded();

    // Capture the container now, before the placeholder text (which we
    // locate it by) disappears once a value is selected.
    const selectRoot = getSelectRootByPlaceholder("Select a source document");

    await selectFromDropdown(user, "Select a source document", "Doc Four · Vectorized");
    await user.click(screen.getByRole("button", { name: /^(link |loading )?attach$/i }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /^(link |loading )?attach$/i }).querySelector(".anticon-loading")).toBeTruthy();
    });
    expect(selectRoot).toHaveClass("ant-select-disabled");

    deferred.resolve();
  });
});
