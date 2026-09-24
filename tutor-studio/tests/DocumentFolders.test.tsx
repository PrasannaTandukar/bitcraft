import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DocumentFolders from "../src/features/editor/components/DocumentFolders.jsx";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------
// __esModule: true avoids TS/CJS interop wrapping issues with the mock
// factory's exports (bit us on an earlier suite this session).

vi.mock("../src/api/client.js", () => ({
  __esModule: true,
  getApiErrorDescription: vi.fn((_error: unknown, fallback: string) => fallback),
}));

vi.mock("../src/features/editor/utils/documentApi.js", () => ({
  __esModule: true,
  createDocumentFolder: vi.fn(),
  deleteDocumentFolder: vi.fn(),
  renameDocumentFolder: vi.fn(),
}));

import { getApiErrorDescription } from "../src/api/client.js";
import {
  createDocumentFolder,
  deleteDocumentFolder,
  renameDocumentFolder,
} from "../src/features/editor/utils/documentApi.js";

const mockGetApiErrorDescription = vi.mocked(getApiErrorDescription);
const mockCreateDocumentFolder = vi.mocked(createDocumentFolder);
const mockDeleteDocumentFolder = vi.mocked(deleteDocumentFolder);
const mockRenameDocumentFolder = vi.mocked(renameDocumentFolder);

const baseFolders = [
  { id: "f1", name: "Contracts" },
  { id: "f2", name: "Invoices" },
];

function setup(overrides: Record<string, any> = {}) {
  const props = {
    folders: baseFolders,
    selectedFolderId: "all",
    onSelectFolder: vi.fn(),
    documentCounts: { f1: 3, f2: 2 },
    totalCount: 10,
    canManage: true,
    canDelete: true,
    onFoldersChanged: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
  const utils = render(<DocumentFolders {...props} />);
  return { ...utils, props };
}

// Tag.CheckableTag doesn't expose a distinguishing role or accessible name,
// and folder names are duplicated on screen (once as a filter tag, once in
// the management List when canManage is on). Scope directly to the
// checkable-tag DOM class and match by contained text instead.
function getCheckableTag(labelSubstring: string): HTMLElement {
  const tags = Array.from(document.querySelectorAll<HTMLElement>(".ant-tag-checkable"));
  const tag = tags.find((el) => el.textContent?.includes(labelSubstring));
  if (!tag) throw new Error(`Checkable tag not found containing: ${labelSubstring}`);
  return tag;
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
});

// ---------------------------------------------------------------------------
// Filter tags (All / Root / per-folder)
// ---------------------------------------------------------------------------

describe("DocumentFolders - filter tags", () => {
  it("shows the All Documents tag with the total count", () => {
    setup({ totalCount: 10 });
    const element = screen.getByTitle('10');
    expect(element).toBeInTheDocument();
  });

  it("computes the Root / Unassigned count as totalCount minus all folder counts", () => {
    setup({ totalCount: 10, documentCounts: { f1: 3, f2: 2 } });
    const tag = getCheckableTag("Root / Unassigned");
    expect(within(tag).getByText("5")).toBeInTheDocument();
  });

  it("clamps the Root / Unassigned count at zero when folder counts exceed the total", () => {
    setup({ totalCount: 2, documentCounts: { f1: 3, f2: 2 } });
    const tag = getCheckableTag("Root / Unassigned");
    expect(within(tag).getByText("0")).toBeInTheDocument();
  });

  it("renders a checkable tag per folder with its own document count", () => {
    setup();
    expect(within(getCheckableTag("Contracts")).getByText("3")).toBeInTheDocument();
    expect(within(getCheckableTag("Invoices")).getByText("2")).toBeInTheDocument();
  });

  it("defaults a folder's count to zero when missing from documentCounts", () => {
    setup({ documentCounts: {} });
    expect(within(getCheckableTag("Contracts")).getByText("0")).toBeInTheDocument();
  });

  it("calls onSelectFolder('all') when the All Documents tag is clicked", async () => {
    const user = userEvent.setup();
    const { props } = setup({ selectedFolderId: "root" });
    await user.click(getCheckableTag("All Documents"));
    expect(props.onSelectFolder).toHaveBeenCalledWith("all");
  });

  it("calls onSelectFolder('root') when the Root / Unassigned tag is clicked", async () => {
    const user = userEvent.setup();
    const { props } = setup({ selectedFolderId: "all" });
    await user.click(getCheckableTag("Root / Unassigned"));
    expect(props.onSelectFolder).toHaveBeenCalledWith("root");
  });

  it("calls onSelectFolder(folder.id) when an unselected folder tag is clicked", async () => {
    const user = userEvent.setup();
    const { props } = setup({ selectedFolderId: "all" });
    await user.click(getCheckableTag("Contracts"));
    expect(props.onSelectFolder).toHaveBeenCalledWith("f1");
  });

  it("calls onSelectFolder('all') when the already-selected folder tag is clicked again", async () => {
    const user = userEvent.setup();
    const { props } = setup({ selectedFolderId: "f1" });
    await user.click(getCheckableTag("Contracts"));
    expect(props.onSelectFolder).toHaveBeenCalledWith("all");
  });
});

// ---------------------------------------------------------------------------
// Management list visibility / permissions
// ---------------------------------------------------------------------------

describe("DocumentFolders - management visibility", () => {
  it("shows the management list and create-folder input when canManage is true", () => {
    setup({ canManage: true });
    expect(screen.getByPlaceholderText("New folder name")).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /rename/i })).toHaveLength(2);
  });

  it("hides the management list and create-folder input when canManage is false", () => {
    setup({ canManage: false });
    expect(screen.queryByPlaceholderText("New folder name")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /rename/i })).not.toBeInTheDocument();
  });

  it("hides the management list when there are no folders, but still shows the create input", () => {
    setup({ canManage: true, folders: [] });
    expect(screen.queryByRole("button", { name: /rename/i })).not.toBeInTheDocument();
    expect(screen.getByPlaceholderText("New folder name")).toBeInTheDocument();
  });

  it("hides Delete buttons when canDelete is false, but keeps Rename", () => {
    setup({ canManage: true, canDelete: false });
    expect(screen.getAllByRole("button", { name: /rename/i })).toHaveLength(2);
    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  it("shows Delete buttons when canDelete is true", () => {
    setup({ canManage: true, canDelete: true });
    expect(screen.getAllByRole("button", { name: /delete/i })).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// Rename flow
// ---------------------------------------------------------------------------

describe("DocumentFolders - rename", () => {
  it("opens an editable input pre-filled with the folder's current name", async () => {
    const user = userEvent.setup();
    setup();
    const renameButtons = screen.getAllByRole("button", { name: /rename/i });
    await user.click(renameButtons[0]);
    expect(screen.getByDisplayValue("Contracts")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /save/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /^cancel$/i })).toBeInTheDocument();
  });

  it("calls renameDocumentFolder with the trimmed new name on Save", async () => {
    mockRenameDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(screen.getAllByRole("button", { name: /rename/i })[0]);

    const input = screen.getByDisplayValue("Contracts");
    await user.clear(input);
    await user.type(input, "  Signed Contracts  ");
    await user.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(mockRenameDocumentFolder).toHaveBeenCalledWith("f1", "Signed Contracts");
    });
    expect(props.onFoldersChanged).toHaveBeenCalled();
  });

  it("returns to the read-only view after a successful rename", async () => {
    mockRenameDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();
    await user.click(screen.getAllByRole("button", { name: /rename/i })[0]);
    await user.click(screen.getByRole("button", { name: /save/i }));

    await waitFor(() => {
      expect(screen.queryByRole("button", { name: /save/i })).not.toBeInTheDocument();
    });
    expect(screen.getAllByRole("button", { name: /rename/i })).toHaveLength(2);
  });

  it("submits the rename when pressing Enter in the input", async () => {
    mockRenameDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();
    await user.click(screen.getAllByRole("button", { name: /rename/i })[0]);
    const input = screen.getByDisplayValue("Contracts");
    await user.type(input, "{Enter}");
    await waitFor(() => expect(mockRenameDocumentFolder).toHaveBeenCalledWith("f1", "Contracts"));
  });

  it("exits editing without renaming when Cancel is clicked", async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getAllByRole("button", { name: /rename/i })[0]);
    await user.click(screen.getByRole("button", { name: /^cancel$/i }));
    expect(mockRenameDocumentFolder).not.toHaveBeenCalled();
    expect(screen.getAllByRole("button", { name: /rename/i })).toHaveLength(2);
  });

  it("shows an error and stays in editing mode when the rename fails", async () => {
    mockRenameDocumentFolder.mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    setup();
    await user.click(screen.getAllByRole("button", { name: /rename/i })[0]);
    await user.click(screen.getByRole("button", { name: /save/i }));

    expect(await screen.findByText("The folder could not be renamed.")).toBeInTheDocument();
    // Still editing: the input and Save/Cancel controls remain.
    expect(screen.getByDisplayValue("Contracts")).toBeInTheDocument();
  });

  it("shows a loading indicator on Save while the rename request is in flight", async () => {
    const deferred = createDeferred<void>();
    mockRenameDocumentFolder.mockReturnValue(deferred.promise as any);
    const user = userEvent.setup();
    setup();
    await user.click(screen.getAllByRole("button", { name: /rename/i })[0]);
    await user.click(screen.getByRole("button", { name: /save/i }));

    const saveButton = await screen.findByRole("button", { name: /save/i });
    expect(saveButton.querySelector(".anticon-loading")).toBeTruthy();

    deferred.resolve();
    await waitFor(() => {
      expect(screen.queryByRole("button", { name: /save/i })).not.toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// Create folder flow
// ---------------------------------------------------------------------------

describe("DocumentFolders - create folder", () => {
  it("disables Create folder until a non-blank name is entered", async () => {
    const user = userEvent.setup();
    setup();
    const createButton = screen.getByRole("button", { name: /create folder/i });
    expect(createButton).toBeDisabled();

    await user.type(screen.getByPlaceholderText("New folder name"), "   ");
    expect(createButton).toBeDisabled();

    await user.type(screen.getByPlaceholderText("New folder name"), "Receipts");
    expect(createButton).toBeEnabled();
  });

  it("calls createDocumentFolder with the trimmed name and clears the input on success", async () => {
    mockCreateDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    const { props } = setup();
    const input = screen.getByPlaceholderText("New folder name");
    await user.type(input, "  Receipts  ");
    await user.click(screen.getByRole("button", { name: /create folder/i }));

    await waitFor(() => expect(mockCreateDocumentFolder).toHaveBeenCalledWith("Receipts"));
    expect(props.onFoldersChanged).toHaveBeenCalled();
    await waitFor(() => expect(screen.getByPlaceholderText("New folder name")).toHaveValue(""));
  });

  it("submits on Enter in the new-folder input", async () => {
    mockCreateDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    setup();
    const input = screen.getByPlaceholderText("New folder name");
    await user.type(input, "Receipts{Enter}");
    await waitFor(() => expect(mockCreateDocumentFolder).toHaveBeenCalledWith("Receipts"));
  });

  it("shows an error and keeps the typed name when creation fails", async () => {
    mockCreateDocumentFolder.mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    setup();
    const input = screen.getByPlaceholderText("New folder name");
    await user.type(input, "Receipts");
    await user.click(screen.getByRole("button", { name: /create folder/i }));

    expect(await screen.findByText("The folder could not be created.")).toBeInTheDocument();
    expect(input).toHaveValue("Receipts");
  });

  it("shows a loading indicator on Create folder while the request is in flight", async () => {
    const deferred = createDeferred<void>();
    mockCreateDocumentFolder.mockReturnValue(deferred.promise as any);
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByPlaceholderText("New folder name"), "Receipts");
    await user.click(screen.getByRole("button", { name: /create folder/i }));

    const createButton = await screen.findByRole("button", { name: /create folder/i });
    expect(createButton.querySelector(".anticon-loading")).toBeTruthy();

    deferred.resolve();
    await waitFor(() => expect(createButton.querySelector(".anticon-loading")).toBeFalsy());
  });
});

// ---------------------------------------------------------------------------
// Delete flow
// ---------------------------------------------------------------------------

describe("DocumentFolders - delete", () => {
  it("calls deleteDocumentFolder after confirming", async () => {
    mockDeleteDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    const { props } = setup({ selectedFolderId: "all" });
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    await user.click(deleteButtons[0]);

    const confirmButton = await screen.findByRole("button", { name: "Delete" });
    await user.click(confirmButton);

    await waitFor(() => expect(mockDeleteDocumentFolder).toHaveBeenCalledWith("f1"));
    expect(props.onFoldersChanged).toHaveBeenCalled();
  });

  it("does not call deleteDocumentFolder if the confirmation is cancelled", async () => {
    const user = userEvent.setup();
    setup();
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    await user.click(deleteButtons[0]);

    const cancelButton = await screen.findByRole("button", { name: /^cancel$/i });
    await user.click(cancelButton);

    expect(mockDeleteDocumentFolder).not.toHaveBeenCalled();
  });

  it("switches selection to 'all' when deleting the currently selected folder", async () => {
    mockDeleteDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    const { props } = setup({ selectedFolderId: "f1" });
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    await user.click(deleteButtons[0]);
    await user.click(await screen.findByRole("button", { name: "Delete" }));

    await waitFor(() => expect(mockDeleteDocumentFolder).toHaveBeenCalled());
    expect(props.onSelectFolder).toHaveBeenCalledWith("all");
  });

  it("does not change selection when deleting a folder that isn't selected", async () => {
    mockDeleteDocumentFolder.mockResolvedValue(undefined);
    const user = userEvent.setup();
    const { props } = setup({ selectedFolderId: "f2" });
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    await user.click(deleteButtons[0]); // deleting f1, selection is on f2
    await user.click(await screen.findByRole("button", { name: "Delete" }));

    await waitFor(() => expect(mockDeleteDocumentFolder).toHaveBeenCalled());
    expect(props.onSelectFolder).not.toHaveBeenCalled();
  });

  it("shows an error when deletion fails", async () => {
    mockDeleteDocumentFolder.mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    setup();
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    await user.click(deleteButtons[0]);
    await user.click(await screen.findByRole("button", { name: "Delete" }));

    expect(await screen.findByText("The folder could not be deleted.")).toBeInTheDocument();
  });

  it("shows a loading indicator on the Delete button while the request is in flight", async () => {
    const deferred = createDeferred<void>();
    mockDeleteDocumentFolder.mockReturnValue(deferred.promise as any);
    const user = userEvent.setup();
    setup();
    const deleteButtons = screen.getAllByRole("button", { name: /delete/i });
    await user.click(deleteButtons[0]);
    await user.click(await screen.findByRole("button", { name: "Delete" }));

    await waitFor(() => {
      expect(deleteButtons[0].querySelector(".anticon-loading")).toBeTruthy();
    });

    deferred.resolve();
  });
});

// ---------------------------------------------------------------------------
// Error alert
// ---------------------------------------------------------------------------

describe("DocumentFolders - error alert", () => {
  it("does not show an alert when there is no error", () => {
    setup();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("clears the error when the alert is dismissed", async () => {
    mockCreateDocumentFolder.mockRejectedValue(new Error("boom"));
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByPlaceholderText("New folder name"), "Receipts");
    await user.click(screen.getByRole("button", { name: /create folder/i }));

    expect(await screen.findByText("The folder could not be created.")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /close/i }));
    expect(screen.queryByText("The folder could not be created.")).not.toBeInTheDocument();
  });
});
