import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DocumentRow from "../src/features/editor/components/DocumentRow.jsx";

// ---------------------------------------------------------------------------
// Fixtures / helpers
// ---------------------------------------------------------------------------

const baseDocument = {
  id: "d1",
  name: "Report.pdf",
  mimeType: "application/pdf",
  hasRawText: true,
  rawTextLength: 1234,
  lastModifiedUtc: "2024-01-15T00:00:00.000Z",
  createdUtc: "2024-01-01T00:00:00.000Z",
};

function setup(overrides: Record<string, any> = {}) {
  const props = {
    document: baseDocument,
    folderName: undefined,
    status: { isVectorized: true, chunkCount: 5 },
    canManage: true,
    canDelete: true,
    onPreview: vi.fn(),
    onReplace: vi.fn(),
    onVectorize: vi.fn(),
    onDelete: vi.fn(),
    onMove: vi.fn(),
    ...overrides,
  };
  const utils = render(<DocumentRow {...props} />);
  return { ...utils, props };
}

// Mirrors the component's own `new Date(value).toLocaleDateString()` so the
// expected string matches regardless of the machine's locale.
function expectedDateText(value: string) {
  return new Date(value).toLocaleDateString();
}

beforeEach(() => {
  vi.clearAllMocks();
});

// ---------------------------------------------------------------------------
// File type icon
// ---------------------------------------------------------------------------

describe("DocumentRow - file type icon", () => {
  it("shows PDF for a pdf mime type", () => {
    setup({ document: { ...baseDocument, mimeType: "application/pdf", name: "file" } });
    expect(screen.getByText("PDF")).toBeInTheDocument();
  });

  it("shows PDF based on a .pdf filename even without a matching mime type", () => {
    setup({ document: { ...baseDocument, mimeType: undefined, name: "Report.PDF" } });
    expect(screen.getByText("PDF")).toBeInTheDocument();
  });

  it("shows DOC for a wordprocessingml mime type", () => {
    setup({
      document: {
        ...baseDocument,
        mimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        name: "file",
      },
    });
    expect(screen.getByText("DOC")).toBeInTheDocument();
  });

  it("shows DOC based on a .docx filename", () => {
    setup({ document: { ...baseDocument, mimeType: undefined, name: "Notes.docx" } });
    expect(screen.getByText("DOC")).toBeInTheDocument();
  });

  it("falls back to TXT for anything else", () => {
    setup({ document: { ...baseDocument, mimeType: "text/plain", name: "notes.txt" } });
    expect(screen.getByText("TXT")).toBeInTheDocument();
  });

  it("falls back to TXT when name is missing entirely", () => {
    setup({ document: { ...baseDocument, mimeType: undefined, name: undefined } });
    expect(screen.getByText("TXT")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Date formatting
// ---------------------------------------------------------------------------

describe("DocumentRow - date formatting", () => {
  it("prefers lastModifiedUtc over createdUtc when both are present", () => {
    setup({
      document: {
        ...baseDocument,
        lastModifiedUtc: "2024-01-15T00:00:00.000Z",
        createdUtc: "2024-01-01T00:00:00.000Z",
      },
    });
    expect(screen.getByText(new RegExp(expectedDateText("2024-01-15T00:00:00.000Z")))).toBeInTheDocument();
  });

  it("falls back to createdUtc when lastModifiedUtc is missing", () => {
    setup({
      document: {
        ...baseDocument,
        lastModifiedUtc: undefined,
        createdUtc: "2024-01-01T00:00:00.000Z",
      },
    });
    expect(screen.getByText(new RegExp(expectedDateText("2024-01-01T00:00:00.000Z")))).toBeInTheDocument();
  });

  it("shows 'Unknown date' when neither date is present", () => {
    setup({ document: { ...baseDocument, lastModifiedUtc: undefined, createdUtc: undefined } });
    expect(screen.getByText(/Unknown date/)).toBeInTheDocument();
  });

  it("shows 'Unknown date' for an unparsable date value", () => {
    setup({ document: { ...baseDocument, lastModifiedUtc: "not-a-date", createdUtc: undefined } });
    expect(screen.getByText(/Unknown date/)).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Basic content rendering
// ---------------------------------------------------------------------------

describe("DocumentRow - content", () => {
  it("renders the document name", () => {
    setup({ document: { ...baseDocument, name: "Q3 Report.pdf" } });
    expect(screen.getByText("Q3 Report.pdf")).toBeInTheDocument();
  });

  it("falls back to 'Unnamed document' when name is missing", () => {
    setup({ document: { ...baseDocument, name: undefined } });
    expect(screen.getByText("Unnamed document")).toBeInTheDocument();
  });

  it("falls back to 'Unknown type' when mimeType is missing", () => {
    setup({ document: { ...baseDocument, mimeType: undefined } });
    expect(screen.getByText(/Unknown type/)).toBeInTheDocument();
  });

  it("includes the folder name in the secondary line when provided", () => {
    setup({ folderName: "Contracts" });
    expect(screen.getByText(/· Contracts/)).toBeInTheDocument();
  });

  it("does not append a folder segment when folderName is not provided", () => {
    setup({ folderName: undefined });
    expect(screen.queryByText(/· undefined/)).not.toBeInTheDocument();
  });

  it("shows the formatted extracted-character count when hasRawText is true", () => {
    setup({ document: { ...baseDocument, hasRawText: true, rawTextLength: 12345 } });
    expect(screen.getByText("12,345 extracted characters")).toBeInTheDocument();
  });

  it("treats a missing rawTextLength as zero", () => {
    setup({ document: { ...baseDocument, hasRawText: true, rawTextLength: undefined } });
    expect(screen.getByText("0 extracted characters")).toBeInTheDocument();
  });

  it("shows 'No extracted text' (in both tags) when hasRawText is false", () => {
    setup({ document: { ...baseDocument, hasRawText: false } });
    // hasRawText=false short-circuits statusLabel() too, so the same text
    // legitimately renders in both the content tag and the status tag.
    expect(screen.getAllByText("No extracted text")).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// Status label logic
// ---------------------------------------------------------------------------

describe("DocumentRow - status label", () => {
  it("shows that the status request failed when status.unavailable is true", () => {
    setup({ document: { ...baseDocument, hasRawText: true }, status: { unavailable: true } });
    expect(screen.getByText("Status check failed")).toBeInTheDocument();
  });

  it("shows 'Needs re-vectorization' when status.isStale is true", () => {
    setup({ document: { ...baseDocument, hasRawText: true }, status: { isStale: true } });
    expect(screen.getByText("Needs re-vectorization")).toBeInTheDocument();
  });

  it("shows 'Vectorized · N chunks' when status.isVectorized is true", () => {
    setup({
      document: { ...baseDocument, hasRawText: true },
      status: { isVectorized: true, chunkCount: 7 },
    });
    expect(screen.getByText("Vectorized · 7 chunks")).toBeInTheDocument();
  });

  it("shows 'Checking vectorization' when status is undefined", () => {
    setup({ document: { ...baseDocument, hasRawText: true }, status: undefined });
    expect(screen.getByText("Checking vectorization")).toBeInTheDocument();
  });

  it("shows 'Not vectorized' when status is an empty object", () => {
    setup({ document: { ...baseDocument, hasRawText: true }, status: {} });
    expect(screen.getByText("Not vectorized")).toBeInTheDocument();
  });

  it("shows 'Not vectorized' (not 'Checking...') when status is null", () => {
    // Edge case: the component's check is `status === undefined`, so a
    // `null` status falls through to the final default branch rather than
    // the "Checking vectorization" branch — worth pinning down explicitly.
    setup({ document: { ...baseDocument, hasRawText: true }, status: null });
    expect(screen.getByText("Not vectorized")).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Action buttons
// ---------------------------------------------------------------------------

describe("DocumentRow - action buttons", () => {
  it("always renders a Preview button and calls onPreview with the document", async () => {
    const user = userEvent.setup();
    const { props } = setup();
    await user.click(screen.getByRole("button", { name: "Preview extracted text" }));
    expect(props.onPreview).toHaveBeenCalledWith(baseDocument);
  });

  it("shows 'Add file content' (not Vectorize) when canManage and hasRawText is false", async () => {
    const user = userEvent.setup();
    const { props } = setup({ document: { ...baseDocument, hasRawText: false }, canManage: true });
    expect(screen.queryByRole("button", { name: /vectorize/i })).not.toBeInTheDocument();
    const addButton = screen.getByRole("button", { name: "Add file content" });
    await user.click(addButton);
    expect(props.onReplace).toHaveBeenCalledWith(expect.objectContaining({ id: "d1" }));
  });

  it("shows 'Vectorize' (not Add file content) when canManage and hasRawText is true and not yet vectorized", async () => {
    const user = userEvent.setup();
    const { props } = setup({
      document: { ...baseDocument, hasRawText: true },
      status: {},
      canManage: true,
    });
    expect(screen.queryByRole("button", { name: "Add file content" })).not.toBeInTheDocument();
    const vectorizeButton = screen.getByRole("button", { name: "Vectorize" });
    await user.click(vectorizeButton);
    expect(props.onVectorize).toHaveBeenCalledWith(baseDocument);
  });

  it("shows 'Re-vectorize' when the document is already vectorized", async () => {
    const user = userEvent.setup();
    const { props } = setup({
      document: { ...baseDocument, hasRawText: true },
      status: { isVectorized: true, chunkCount: 3 },
      canManage: true,
    });
    const button = screen.getByRole("button", { name: "Re-vectorize" });
    await user.click(button);
    expect(props.onVectorize).toHaveBeenCalledWith(baseDocument);
  });

  it("shows a Move to folder button when canManage is true and onMove is provided", async () => {
    const user = userEvent.setup();
    const { props } = setup({ canManage: true, onMove: vi.fn() });
    const moveButton = screen.getByRole("button", { name: "Move to folder" });
    await user.click(moveButton);
    expect(props.onMove).toHaveBeenCalledWith(baseDocument);
  });

  it("hides the Move to folder button when onMove is not provided", () => {
    setup({ canManage: true, onMove: undefined });
    expect(screen.queryByRole("button", { name: "Move to folder" })).not.toBeInTheDocument();
  });

  it("hides Add/Vectorize/Move buttons entirely when canManage is false", () => {
    setup({ canManage: false, onMove: vi.fn() });
    expect(screen.queryByRole("button", { name: /add file content/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /vectorize/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Move to folder" })).not.toBeInTheDocument();
  });

  it("shows a Delete button and calls onDelete when canDelete is true", async () => {
    const user = userEvent.setup();
    const { props } = setup({ canDelete: true });
    const deleteButton = screen.getByRole("button", { name: "Delete document" });
    await user.click(deleteButton);
    expect(props.onDelete).toHaveBeenCalledWith(baseDocument);
  });

  it("hides the Delete button when canDelete is false", () => {
    setup({ canDelete: false });
    expect(screen.queryByRole("button", { name: "Delete document" })).not.toBeInTheDocument();
  });

  it("renders only Preview and Delete when canManage is false and canDelete is true", () => {
    setup({ canManage: false, canDelete: true });
    expect(screen.getAllByRole("button")).toHaveLength(2);
  });

  it("renders Preview, Vectorize, Move, and Delete when fully permitted with extracted text", () => {
    setup({
      document: { ...baseDocument, hasRawText: true },
      canManage: true,
      canDelete: true,
      onMove: vi.fn(),
    });
    expect(screen.getAllByRole("button")).toHaveLength(4);
  });

  it("renders Preview, Add file content, Move, and Delete when fully permitted without extracted text", () => {
    setup({
      document: { ...baseDocument, hasRawText: false },
      canManage: true,
      canDelete: true,
      onMove: vi.fn(),
    });
    expect(screen.getAllByRole("button")).toHaveLength(4);
  });
});
