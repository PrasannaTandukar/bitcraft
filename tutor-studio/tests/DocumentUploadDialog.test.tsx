import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DocumentUploadDialog from "../src/features/editor/components/DocumentUploadDialog.jsx";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock("../src/api/client.js", () => ({
  __esModule: true,
  getApiErrorDescription: vi.fn((_error: unknown, fallback: string) => fallback),
}));

vi.mock("../src/features/editor/utils/documentApi.js", () => ({
  __esModule: true,
  acceptedDocumentExtensions: ".txt,.md,.csv,.json,.pdf,.docx",
  defaultMaximumUploadLabel: "20 MB",
  replaceDocumentContent: vi.fn(),
  uploadDocument: vi.fn(),
}));

import { getApiErrorDescription } from "../src/api/client.js";
import {
  defaultMaximumUploadLabel,
  replaceDocumentContent,
  uploadDocument,
} from "../src/features/editor/utils/documentApi.js";

const mockGetApiErrorDescription = vi.mocked(getApiErrorDescription);
const mockReplaceDocumentContent = vi.mocked(replaceDocumentContent);
const mockUploadDocument = vi.mocked(uploadDocument);

// ---------------------------------------------------------------------------
// Fixtures / helpers
// ---------------------------------------------------------------------------

const baseFolders = [
  { id: "f1", name: "Contracts" },
  { id: "f2", name: "Invoices" },
];

const existingDocument = { id: "doc1", folderId: "f1" };

function setup(overrides: Record<string, any> = {}) {
  const props = {
    document: null,
    folders: baseFolders,
    onClose: vi.fn(),
    onSaved: vi.fn(),
    ...overrides,
  };
  const utils = render(<DocumentUploadDialog {...props} />);
  return { ...utils, props };
}

function makeFile(name = "notes.txt", type = "text/plain") {
  return new File(["file contents"], name, { type });
}

function getFileInput(): HTMLInputElement {
  const input = document.querySelector('input[type="file"]');
  if (!input) throw new Error("File input not found");
  return input as HTMLInputElement;
}

async function uploadFile(user: ReturnType<typeof userEvent.setup>, file: File) {
  await user.upload(getFileInput(), file);
}

// Same rc-select quirk as earlier suites: the real, clickable option lives
// in the portaled `.ant-select-dropdown` popup, and only that real option
// carries a `title` attribute (the hidden accessibility mirror does not).
async function selectFolder(user: ReturnType<typeof userEvent.setup>, folderName: string) {
  await user.click(screen.getByLabelText("Folder"));
  const dropdown = await waitFor(() => {
    const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
    if (!el) throw new Error("Select dropdown is not open yet");
    return el as HTMLElement;
  });
  const option = within(dropdown).getByTitle(folderName);
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
});

// ---------------------------------------------------------------------------
// Rendering variations
// ---------------------------------------------------------------------------

describe("DocumentUploadDialog - rendering", () => {
  it("shows the upload title, OK text, and no warning when adding a new document", () => {
    setup({ document: null });
    expect(screen.getByText("Add file")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Upload file" })).toBeInTheDocument();
    expect(screen.queryByText(/Replacing content removes existing knowledge chunks/i)).not.toBeInTheDocument();
  });

  it("shows the replace title, OK text, and warning alert when a document is provided", () => {
    setup({ document: existingDocument });
    expect(screen.getByText("Add or replace file content")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Replace content" })).toBeInTheDocument();
    expect(screen.getByText(/Replacing content removes existing knowledge chunks/i)).toBeInTheDocument();
  });

  it("shows the Folder select only when adding a new document", () => {
    const { rerender } = setup({ document: null });
    expect(screen.getByLabelText("Folder")).toBeInTheDocument();

    rerender(
      <DocumentUploadDialog document={existingDocument} folders={baseFolders} onClose={vi.fn()} onSaved={vi.fn()} />
    );
    expect(screen.queryByLabelText("Folder")).not.toBeInTheDocument();
  });

  it("shows the supported-formats helper text using the configured max size label", () => {
    setup();
    expect(
      screen.getByText(
        `Supported: TXT, Markdown, CSV, JSON, PDF, and DOCX. Maximum size: ${defaultMaximumUploadLabel}. OCR is not supported.`
      )
    ).toBeInTheDocument();
  });

  it("calls onClose when the header close icon is clicked while idle", async () => {
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(screen.getByRole("button", { name: "Close" }));
    expect(props.onClose).toHaveBeenCalledTimes(1);
  });
});

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

describe("DocumentUploadDialog - validation", () => {
  it("shows a required-file validation error when submitting without a file", async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole("button", { name: "Upload file" }));
    expect(await screen.findByText("Select a supported file.")).toBeInTheDocument();
    expect(mockUploadDocument).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Successful upload / replace flows
// ---------------------------------------------------------------------------

describe("DocumentUploadDialog - successful submission", () => {
  it("uploads a new document with no folder selected", async () => {
    mockUploadDocument.mockResolvedValue({ id: "newDoc" });
    const user = userEvent.setup();
    const { props } = setup({ document: null });

    await uploadFile(user, makeFile());
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    await waitFor(() => expect(mockUploadDocument).toHaveBeenCalled());
    const [fileArg, folderArg] = mockUploadDocument.mock.calls[0];
    expect(fileArg).toBeInstanceOf(File);
    expect((fileArg as File).name).toBe("notes.txt");
    expect(folderArg).toBeNull();

    await waitFor(() => expect(props.onSaved).toHaveBeenCalledWith({ id: "newDoc" }, false));
  });

  it("uploads a new document with a selected folder", async () => {
    mockUploadDocument.mockResolvedValue({ id: "newDoc" });
    const user = userEvent.setup();
    setup({ document: null });

    await uploadFile(user, makeFile());
    await selectFolder(user, "Invoices");
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    await waitFor(() => {
      expect(mockUploadDocument).toHaveBeenCalledWith(expect.any(File), "f2", expect.any(AbortSignal));
    });
  });

  it("replaces an existing document's content", async () => {
    mockReplaceDocumentContent.mockResolvedValue({ id: "doc1" });
    const user = userEvent.setup();
    const { props } = setup({ document: existingDocument });

    await uploadFile(user, makeFile("replacement.pdf", "application/pdf"));
    await user.click(screen.getByRole("button", { name: "Replace content" }));

    await waitFor(() => {
      expect(mockReplaceDocumentContent).toHaveBeenCalledWith("doc1", expect.any(File), expect.any(AbortSignal));
    });
    await waitFor(() => expect(props.onSaved).toHaveBeenCalledWith({ id: "doc1" }, true));
  });
});

// ---------------------------------------------------------------------------
// Busy / loading state
// ---------------------------------------------------------------------------

describe("DocumentUploadDialog - busy state", () => {
  it("shows a loading OK button, hides the close icon, and shows the uploading message while in flight", async () => {
    const deferred = createDeferred<{ id: string }>();
    mockUploadDocument.mockReturnValue(deferred.promise as any);
    const user = userEvent.setup();
    setup();

    await uploadFile(user, makeFile());
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /upload file/i }).querySelector(".anticon-loading")).toBeTruthy();
    });
    expect(screen.queryByRole("button", { name: "Close" })).not.toBeInTheDocument();
    expect(screen.getByText(/Uploading and extracting text/i)).toBeInTheDocument();

    deferred.resolve({ id: "newDoc" });
    await waitFor(() => expect(screen.getByRole("button", { name: "Close" })));
  });
});

// ---------------------------------------------------------------------------
// Error handling
// ---------------------------------------------------------------------------

describe("DocumentUploadDialog - error handling", () => {
  it("shows the API error fallback and does not call onSaved when the request rejects outright", async () => {
    mockUploadDocument.mockRejectedValue(new Error("network down"));
    const user = userEvent.setup();
    const { props } = setup();

    await uploadFile(user, makeFile());
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    expect(
      await screen.findByText("The file could not be uploaded or its text could not be extracted.")
    ).toBeInTheDocument();
    expect(props.onSaved).not.toHaveBeenCalled();
  });

  it("re-enables the close icon after a failed request", async () => {
    mockUploadDocument.mockRejectedValue(new Error("network down"));
    const user = userEvent.setup();
    setup();

    await uploadFile(user, makeFile());
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    await screen.findByText("The file could not be uploaded or its text could not be extracted.");
    expect(screen.getByRole("button", { name: "Close" })).toBeInTheDocument();
  });

  it("ignores AbortError rejections: no error alert, busy resets, onSaved not called", async () => {
    const abortError = new Error("aborted");
    abortError.name = "AbortError";
    mockUploadDocument.mockRejectedValue(abortError);
    const user = userEvent.setup();
    const { props } = setup();

    await uploadFile(user, makeFile());
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    // Give the rejected promise a tick to settle.
    await waitFor(() => expect(screen.getByRole("button", { name: "Close" })).toBeInTheDocument());
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(props.onSaved).not.toHaveBeenCalled();
  });

  it("still calls onSaved with a shape-invalid response, alongside the error alert (documents current behavior)", async () => {
    // The component assigns `saved` before validating its shape; throwing on
    // an invalid shape does not clear that assignment, so the final
    // `if (saved) onSaved(...)` still fires with the invalid object. This
    // looks like it may be unintentional, but it's the real current
    // behavior, so it's worth having a test that would catch a change here.
    mockUploadDocument.mockResolvedValue({ notAnId: true });
    const user = userEvent.setup();
    const { props } = setup();

    await uploadFile(user, makeFile());
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    expect(
      await screen.findByText("The file could not be uploaded or its text could not be extracted.")
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(props.onSaved).toHaveBeenCalledWith({ notAnId: true }, false);
    });
  });
});

// ---------------------------------------------------------------------------
// Unmount cleanup
// ---------------------------------------------------------------------------

describe("DocumentUploadDialog - cleanup", () => {
  it("aborts the in-flight request when unmounted", async () => {
    let capturedSignal: AbortSignal | undefined;
    mockUploadDocument.mockImplementation((_file: File, _folderId: string | null, signal: AbortSignal) => {
      capturedSignal = signal;
      return new Promise(() => {});
    });
    const user = userEvent.setup();
    const { unmount } = setup();

    await uploadFile(user, makeFile());
    await user.click(screen.getByRole("button", { name: "Upload file" }));

    await waitFor(() => expect(capturedSignal).toBeDefined());
    expect(capturedSignal?.aborted).toBe(false);
    unmount();
    expect(capturedSignal?.aborted).toBe(true);
  });
});
