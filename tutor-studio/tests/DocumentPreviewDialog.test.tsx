import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DocumentPreviewDialog from "../src/features/editor/components/DocumentPreviewDialog.jsx";

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

vi.mock("../src/api/client.js", () => ({
  __esModule: true,
  getApiErrorDescription: vi.fn((_error: unknown, fallback: string) => fallback),
}));

vi.mock("../src/features/editor/utils/documentApi.js", () => ({
  __esModule: true,
  getDocument: vi.fn(),
}));

import { getApiErrorDescription } from "../src/api/client.js";
import { getDocument } from "../src/features/editor/utils/documentApi.js";

const mockGetApiErrorDescription = vi.mocked(getApiErrorDescription);
const mockGetDocument = vi.mocked(getDocument);

const baseDocument = { id: "doc1" };

const baseDetail = {
  name: "Report.pdf",
  mimeType: "application/pdf",
  rawText: "Hello world",
};

function setup(overrides: Record<string, any> = {}) {
  const props = {
    document: baseDocument,
    canManage: false,
    onClose: vi.fn(),
    onReplace: vi.fn(),
    ...overrides,
  };
  const utils = render(<DocumentPreviewDialog {...props} />);
  return { ...utils, props };
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

// The Drawer's built-in header close icon carries aria-label="Close", and
// the footer's plain <Button>Close</Button> computes the same accessible
// name via its text content — an unscoped role query for "Close" is
// ambiguous between the two. Scope the footer button to its own container.
function getFooterCloseButton(): HTMLElement {
  const footer = document.querySelector(".ant-drawer-footer") as HTMLElement;
  return within(footer).getByRole("button", { name: "Close" });
}

beforeEach(() => {
  vi.clearAllMocks();
  mockGetApiErrorDescription.mockImplementation((_error: unknown, fallback: string) => fallback);
});

// ---------------------------------------------------------------------------
// Fetching / loading / error states
// ---------------------------------------------------------------------------

describe("DocumentPreviewDialog - loading and fetch", () => {
  it("calls getDocument with the document id and an AbortSignal", () => {
    mockGetDocument.mockReturnValue(new Promise(() => {}));
    setup({ document: { id: "doc1" } });
    expect(mockGetDocument).toHaveBeenCalledWith("doc1", expect.any(AbortSignal));
  });

  it("shows a loading spinner while the request is in flight", () => {
    mockGetDocument.mockReturnValue(new Promise(() => {}));
    setup();
    // Drawer content is portaled to document.body by default, so it sits
    // outside the render() container — query from `document` instead.
    expect(document.querySelector(".workspace-loading")).toBeInTheDocument();
  });

  it("hides the loading spinner once the request resolves", async () => {
    mockGetDocument.mockResolvedValue(baseDetail);
    setup();
    await waitFor(() => {
      expect(document.querySelector(".workspace-loading")).not.toBeInTheDocument();
    });
  });

  it("refetches when the document id changes", async () => {
    mockGetDocument.mockResolvedValue(baseDetail);
    const { rerender } = render(
      <DocumentPreviewDialog document={{ id: "doc1" }} canManage={false} onClose={vi.fn()} onReplace={vi.fn()} />
    );
    await waitFor(() => expect(mockGetDocument).toHaveBeenCalledWith("doc1", expect.any(AbortSignal)));

    rerender(
      <DocumentPreviewDialog document={{ id: "doc2" }} canManage={false} onClose={vi.fn()} onReplace={vi.fn()} />
    );
    await waitFor(() => expect(mockGetDocument).toHaveBeenCalledWith("doc2", expect.any(AbortSignal)));
  });

  it("aborts the in-flight request when unmounted", () => {
    let capturedSignal: AbortSignal | undefined;
    mockGetDocument.mockImplementation((_id: string, signal: AbortSignal) => {
      capturedSignal = signal;
      return new Promise(() => {});
    });
    const { unmount } = setup();
    expect(capturedSignal?.aborted).toBe(false);
    unmount();
    expect(capturedSignal?.aborted).toBe(true);
  });

  it("shows an error alert when the response is not an object", async () => {
    mockGetDocument.mockResolvedValue(null);
    setup();
    expect(await screen.findByText("The document response was invalid.")).toBeInTheDocument();
  });

  it("shows an error alert when the response is a primitive", async () => {
    mockGetDocument.mockResolvedValue("not-an-object");
    setup();
    expect(await screen.findByText("The document response was invalid.")).toBeInTheDocument();
  });

  it("shows an error alert with the API error fallback when the request rejects", async () => {
    mockGetDocument.mockRejectedValue(new Error("network down"));
    setup();
    expect(await screen.findByText("The extracted text could not be loaded.")).toBeInTheDocument();
  });

  it("ignores AbortError rejections and does not show an error", async () => {
    const abortError = new Error("aborted");
    abortError.name = "AbortError";
    mockGetDocument.mockRejectedValue(abortError);
    setup();

    // Give the rejected promise a tick to settle.
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(mockGetApiErrorDescription).not.toHaveBeenCalled();
    // Still in the loading state, since neither detail nor error was set.
    expect(document.querySelector(".workspace-loading")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Detail rendering
// ---------------------------------------------------------------------------

describe("DocumentPreviewDialog - detail rendering", () => {
  it("renders the filename, mime type, and character count", async () => {
    mockGetDocument.mockResolvedValue(baseDetail);
    setup();
    expect(await screen.findByText("Report.pdf")).toBeInTheDocument();
    expect(screen.getByText("application/pdf")).toBeInTheDocument();
    expect(screen.getByText(String("Hello world".length))).toBeInTheDocument();
  });

  it("falls back to 'Unnamed document' and 'Unknown' when name/mimeType are missing", async () => {
    mockGetDocument.mockResolvedValue({ rawText: "" });
    setup();
    expect(await screen.findByText("Unnamed document")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toBeInTheDocument();
  });

  it("renders the extracted text when rawText is non-empty", async () => {
    mockGetDocument.mockResolvedValue(baseDetail);
    setup();
    expect(await screen.findByText("Hello world")).toBeInTheDocument();
    expect(screen.queryByText("No extracted text is stored for this document")).not.toBeInTheDocument();
  });

  it("shows an empty state when rawText is empty", async () => {
    mockGetDocument.mockResolvedValue({ name: "Empty.pdf", mimeType: "application/pdf", rawText: "" });
    setup();
    expect(await screen.findByText("No extracted text is stored for this document")).toBeInTheDocument();
  });

  it("shows an empty state when rawText is missing entirely", async () => {
    mockGetDocument.mockResolvedValue({ name: "NoText.pdf", mimeType: "application/pdf" });
    setup();
    expect(await screen.findByText("No extracted text is stored for this document")).toBeInTheDocument();
    // Character count should read 0, not throw, for a non-string rawText.
    expect(screen.getByText("0")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Footer actions
// ---------------------------------------------------------------------------

describe("DocumentPreviewDialog - footer actions", () => {
  it("always shows the Close button and calls onClose when clicked", async () => {
    mockGetDocument.mockReturnValue(new Promise(() => {}));
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(getFooterCloseButton());
    expect(props.onClose).toHaveBeenCalledTimes(1);
  });

  it("calls onClose when the drawer's header close icon is clicked", async () => {
    mockGetDocument.mockReturnValue(new Promise(() => {}));
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(screen.getByLabelText("Close"));
    expect(props.onClose).toHaveBeenCalledTimes(1);
  });

  it("does not show a Replace/Add button while still loading, even if canManage is true", () => {
    mockGetDocument.mockReturnValue(new Promise(() => {}));
    setup({ canManage: true });
    expect(screen.queryByRole("button", { name: /file content/i })).not.toBeInTheDocument();
  });

  it("does not show a Replace/Add button when canManage is false", async () => {
    mockGetDocument.mockResolvedValue(baseDetail);
    setup({ canManage: false });
    await screen.findByText("Report.pdf");
    expect(screen.queryByRole("button", { name: /file content/i })).not.toBeInTheDocument();
  });

  it("shows 'Add file content' when canManage is true and there is no extracted text", async () => {
    mockGetDocument.mockResolvedValue({ name: "Empty.pdf", mimeType: "application/pdf", rawText: "" });
    setup({ canManage: true });
    expect(await screen.findByRole("button", { name: /add file content/i })).toBeInTheDocument();
  });

  it("shows 'Replace file content' when canManage is true and extracted text exists", async () => {
    mockGetDocument.mockResolvedValue(baseDetail);
    setup({ canManage: true });
    expect(await screen.findByRole("button", { name: /replace file content/i })).toBeInTheDocument();
  });

  it("calls onReplace with the document prop when the Replace/Add button is clicked", async () => {
    mockGetDocument.mockResolvedValue(baseDetail);
    const user = userEvent.setup();
    const { props } = setup({ canManage: true, document: baseDocument });
    const replaceButton = await screen.findByRole("button", { name: /replace file content/i });
    await user.click(replaceButton);
    expect(props.onReplace).toHaveBeenCalledWith(baseDocument);
  });
});
