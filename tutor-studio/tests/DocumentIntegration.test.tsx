import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import FilesPage from "../src/pages/editor/FilesPage.jsx";

import {
  createDocument,
  deleteDocument,
  getDocument,
  getVectorizationStatus,
  listDocumentFolders,
  listDocuments,
  listEmbeddingModels,
  moveDocument,
  replaceDocumentContent,
  uploadDocument,
  vectorizeDocument,
  createDocumentFolder,
  renameDocumentFolder,
  deleteDocumentFolder,
} from "../src/features/editor/utils/documentApi.js";

import useRolePermissions from "../src/hooks/useRolePermissions.js";

vi.mock("../src/features/editor/utils/documentApi.js", () => ({
  acceptedDocumentExtensions: ".txt,.md,.csv,.json,.pdf,.docx",
  defaultMaximumUploadLabel: "10 MB",

  listDocuments: vi.fn(),
  listDocumentFolders: vi.fn(),
  getDocument: vi.fn(),
  uploadDocument: vi.fn(),
  replaceDocumentContent: vi.fn(),
  deleteDocument: vi.fn(),
  listEmbeddingModels: vi.fn(),
  getVectorizationStatus: vi.fn(),
  vectorizeDocument: vi.fn(),
  moveDocument: vi.fn(),
  createDocumentFolder: vi.fn(),
  renameDocumentFolder: vi.fn(),
  deleteDocumentFolder: vi.fn(),
  createDocument: vi.fn(),
}));

vi.mock("../src/hooks/useRolePermissions.js", () => ({
  default: vi.fn(),
}));

const mockedListDocuments = vi.mocked(listDocuments);
const mockedListDocumentFolders = vi.mocked(listDocumentFolders);
const mockedGetDocument = vi.mocked(getDocument);
const mockedCreateDocument = vi.mocked(createDocument);
const mockedGetVectorizationStatus = vi.mocked(getVectorizationStatus);
const mockedListEmbeddingModels = vi.mocked(listEmbeddingModels);
const mockedVectorizeDocument = vi.mocked(vectorizeDocument);
const mockedUseRolePermissions = vi.mocked(useRolePermissions);

const baseDocument = {
  id: "doc-1",
  name: "React Testing Guide.md",
  mimeType: "text/markdown",
  rawTextLength: 53,
  hasRawText: true,
  folderId: null,
  createdUtc: "2026-09-10T08:00:00Z",
  lastModifiedUtc: "2026-09-10T08:00:00Z",
  vectorization: {
    isVectorized: false,
    isStale: false,
    chunkCount: 0,
    embeddingModelId: null,
    embeddingModelName: null,
    providerModelName: null,
    embeddingDimensions: null,
  },
};

const baseFolder = {
  id: "folder-1",
  name: "Testing",
};

const initialStatus = {
  hasRawText: true,
  isVectorized: false,
  isStale: false,
  chunkCount: 0,
  embeddingModelId: null,
  lastVectorizedUtc: null,
};

const vectorizedStatus = {
  hasRawText: true,
  isVectorized: true,
  isStale: false,
  chunkCount: 3,
  embeddingModelId: "model-1",
  lastVectorizedUtc: "2026-09-11T08:00:00Z",
};

const embeddingModels = [
  {
    id: "model-1",
    name: "Nomic Embed",
    providerModelName: "nomic-embed-text",
    embeddingDimensions: 768,
    runtimeEngineName: "Ollama",
    isEnabled: true,
    isRuntimeEngineEnabled: true,
  },
];

function configureDefaultMocks() {
  mockedUseRolePermissions.mockReturnValue({
    canWrite: true,
    canDelete: true,
    canManage: true,
  } as any);

  mockedListDocuments.mockResolvedValue([baseDocument] as any);

  mockedListDocumentFolders.mockResolvedValue([baseFolder] as any);

  // The list response supplies Browse state. The individual endpoint remains
  // mocked for the dialog's focused initial/post-vectorization refreshes.
  mockedListDocuments.mockImplementation(async () => [{
    ...baseDocument,
    vectorization: mockedVectorizeDocument.mock.calls.length > 0 ? vectorizedStatus : initialStatus,
  }] as any);
  mockedGetVectorizationStatus.mockImplementation(async () => {
    if (mockedVectorizeDocument.mock.calls.length > 0) {
      return vectorizedStatus as any;
    }

    return initialStatus as any;
  });

  mockedListEmbeddingModels.mockResolvedValue(
    embeddingModels as any,
  );

  /*
   * Default document detail.
   *
   * Individual tests can override this implementation when they need
   * to return the document that was just created.
   */
  mockedGetDocument.mockImplementation(async (documentId) => {
    if (documentId === "doc-2") {
      return {
        id: "doc-2",
        name: "Testing Notes.md",
        mimeType: "text/markdown",
        rawText:
          "This document contains notes about testing React applications.",
        rawTextLength: 60,
        hasRawText: true,
        folderId: "folder-1",
        createdUtc: "2026-09-11T08:00:00Z",
        lastModifiedUtc: "2026-09-11T08:00:00Z",
      } as any;
    }

    return {
      ...baseDocument,
      rawText:
        "# React Testing\n\nThis document explains how to write tests with React Testing Library.",
    } as any;
  });
}

describe("Document integration workflow", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    configureDefaultMocks();
  });

  it("shows a useful documents-list error when the combined metadata request fails", async () => {
    mockedListDocuments.mockRejectedValue(new Error("service unavailable"));

    render(<FilesPage />);

    expect(await screen.findByText("Documents could not be loaded.")).toBeInTheDocument();
    expect(mockedGetVectorizationStatus).not.toHaveBeenCalled();
  });

  it("loads vectorization states for 15 documents from the list response without per-row status requests", async () => {
    const documents = Array.from({ length: 15 }, (_, index) => ({
      ...baseDocument,
      id: `doc-${index + 1}`,
      name: `Document ${index + 1}.md`,
      vectorization: index === 0
        ? { ...vectorizedStatus, chunkCount: 4 }
        : index === 1
          ? { ...initialStatus }
          : index === 2
            ? { ...vectorizedStatus, isStale: true, chunkCount: 6 }
            : index === 3
              ? null
            : { ...initialStatus },
    }));
    mockedListDocuments.mockResolvedValue(documents as any);

    render(<FilesPage />);

    const firstRow = (await screen.findByText("Document 1.md")).closest(".document-list-item") as HTMLElement;
    const secondRow = screen.getByText("Document 2.md").closest(".document-list-item") as HTMLElement;
    const thirdRow = screen.getByText("Document 3.md").closest(".document-list-item") as HTMLElement;
    const fourthRow = screen.getByText("Document 4.md").closest(".document-list-item") as HTMLElement;
    expect(within(firstRow).getByText("Vectorized · 4 chunks")).toBeInTheDocument();
    expect(within(secondRow).getByText("Not vectorized")).toBeInTheDocument();
    expect(within(thirdRow).getByText("Needs re-vectorization")).toBeInTheDocument();
    expect(within(fourthRow).getByText("Status check failed")).toBeInTheDocument();
    expect(screen.getByText("Vectorization status could not be checked for 1 document.")).toBeInTheDocument();
    expect(mockedListDocuments).toHaveBeenCalledTimes(1);
    expect(mockedGetVectorizationStatus).not.toHaveBeenCalled();
  });

  it("loads documents, creates a document, and previews its extracted text", async () => {
    const user = userEvent.setup();

    const createdDocument = {
      id: "doc-2",
      name: "Testing Notes.md",
      mimeType: "text/markdown",
      rawTextLength: 60,
      hasRawText: true,
      folderId: "folder-1",
      createdUtc: "2026-09-11T08:00:00Z",
      lastModifiedUtc: "2026-09-11T08:00:00Z",
    };

    mockedCreateDocument.mockResolvedValue(
      createdDocument as any,
    );

    render(<FilesPage />);

    // ------------------------------------------------------------
    // 1. Initial documents load
    // ------------------------------------------------------------

    expect(
      await screen.findByText("React Testing Guide.md"),
    ).toBeInTheDocument();

    expect(mockedListDocuments).toHaveBeenCalled();
    expect(mockedListDocumentFolders).toHaveBeenCalled();

    // ------------------------------------------------------------
    // 2. Open Create document dialog
    // ------------------------------------------------------------

    await user.click(
      screen.getByRole("button", {
        name: "Create document",
      }),
    );

    const createDialog = await screen.findByRole("dialog", {
      name: /create document/i,
    });

    // ------------------------------------------------------------
    // 3. Enter document information
    // ------------------------------------------------------------

    const nameInput = within(createDialog).getByPlaceholderText(
      "My document",
    );

    const textInput = within(createDialog).getByPlaceholderText(
      "Paste or type the document text here",
    );

    await user.type(
      nameInput,
      "Testing Notes.md",
    );

    const rawText =
      "This document contains notes about testing React applications.";

    await user.type(textInput, rawText);

    // ------------------------------------------------------------
    // 4. Select destination folder
    // ------------------------------------------------------------

    const folderSelect = within(createDialog).getByRole(
      "combobox",
    );

    await user.click(folderSelect);

    const folderOption = await screen.findByText(
      "Testing",
      {
        selector: ".ant-select-item-option-content",
      },
    );

    await user.click(folderOption);

    // ------------------------------------------------------------
    // 5. Create document
    // ------------------------------------------------------------

    await user.click(
      within(createDialog).getByRole("button", {
        name: "Create document",
      }),
    );

    await waitFor(() => {
      expect(mockedCreateDocument).toHaveBeenCalledWith(
        "Testing Notes.md",
        rawText,
        "folder-1",
      );
    });

    // ------------------------------------------------------------
    // 6. New document appears in FilesPage
    // ------------------------------------------------------------

    expect(
      await screen.findByText("Testing Notes.md"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Document created."),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // 7. Find the newly-created document row
    // ------------------------------------------------------------

    const documentElement = screen.getByText(
      "Testing Notes.md",
    );

    const documentRow = documentElement.closest(
      ".document-list-item",
    );

    expect(documentRow).not.toBeNull();

    // ------------------------------------------------------------
    // 8. Open extracted-text preview
    // ------------------------------------------------------------

    await user.click(
      within(documentRow as HTMLElement).getByRole(
        "button",
        {
          name: "Preview extracted text",
        },
      ),
    );

    const previewDialog = await screen.findByRole(
      "dialog",
      {
        name: /extracted text preview/i,
      },
    );

    // ------------------------------------------------------------
    // 9. Confirm correct document was requested
    // ------------------------------------------------------------

    expect(mockedGetDocument).toHaveBeenCalledWith(
      "doc-2",
      expect.any(AbortSignal),
    );

    // ------------------------------------------------------------
    // 10. Confirm preview displays the created document
    // ------------------------------------------------------------

    expect(
      await within(previewDialog).findByText(
        "Testing Notes.md",
      ),
    ).toBeInTheDocument();

    expect(
      within(previewDialog).getByText(
        "text/markdown",
      ),
    ).toBeInTheDocument();

    expect(
      within(previewDialog).getByText("62")
    ).toBeInTheDocument();

    expect(
      within(previewDialog).getByText(
        rawText,
      ),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // 11. Close preview
    // ------------------------------------------------------------

    const footer = previewDialog.querySelector(
      ".ant-drawer-footer",
    );

    expect(footer).not.toBeNull();

    await user.click(
      within(footer as HTMLElement).getByRole(
        "button",
        {
          name: "Close",
        },
      ),
    );

    await waitFor(() => {
      expect(
        screen.queryByRole("dialog", {
          name: /extracted text preview/i,
        }),
      ).not.toBeInTheDocument();
    });
  });

  it("handles document creation failures", async () => {
    const user = userEvent.setup();

    mockedCreateDocument.mockRejectedValue(
      new Error(
        "The document service is unavailable.",
      ),
    );

    render(<FilesPage />);

    expect(
      await screen.findByText(
        "React Testing Guide.md",
      ),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // Open create dialog
    // ------------------------------------------------------------

    await user.click(
      screen.getByRole("button", {
        name: "Create document",
      }),
    );

    const createDialog = await screen.findByRole(
      "dialog",
      {
        name: /create document/i,
      },
    );

    // ------------------------------------------------------------
    // Enter invalid/failing document
    // ------------------------------------------------------------

    await user.type(
      within(createDialog).getByPlaceholderText(
        "My document",
      ),
      "Broken Document.md",
    );

    await user.type(
      within(createDialog).getByPlaceholderText(
        "Paste or type the document text here",
      ),
      "This document should fail to create.",
    );

    // ------------------------------------------------------------
    // Submit
    // ------------------------------------------------------------

    await user.click(
      within(createDialog).getByRole(
        "button",
        {
          name: "Create document",
        },
      ),
    );

    // ------------------------------------------------------------
    // Error is shown inside dialog
    // ------------------------------------------------------------

    expect(
      await within(createDialog).findByText(
        "The document service is unavailable.",
      ),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // Failed document is not added to workspace
    // ------------------------------------------------------------

    expect(
      screen.queryByText("Broken Document.md"),
    ).not.toBeInTheDocument();
  });

  it("handles document preview failures", async () => {
    const user = userEvent.setup();

    mockedGetDocument.mockRejectedValue(
      new Error(
        "Preview could not be loaded.",
      ),
    );

    render(<FilesPage />);

    // ------------------------------------------------------------
    // Find existing document
    // ------------------------------------------------------------

    const documentElement =
      await screen.findByText(
        "React Testing Guide.md",
      );

    const documentRow =
      documentElement.closest(
        ".document-list-item",
      );

    expect(documentRow).not.toBeNull();

    // ------------------------------------------------------------
    // Open preview
    // ------------------------------------------------------------

    await user.click(
      within(documentRow as HTMLElement).getByRole(
        "button",
        {
          name: "Preview extracted text",
        },
      ),
    );

    const previewDialog =
      await screen.findByRole("dialog", {
        name: /extracted text preview/i,
      });

    // ------------------------------------------------------------
    // Error should be displayed
    // ------------------------------------------------------------

    const alert =
      await within(previewDialog).findByRole(
        "alert",
      );

    expect(alert).toHaveTextContent(
      "The extracted text could not be loaded.",
    );

    // The preview should not display the raw document
    // after the request fails.
    expect(
      within(previewDialog).queryByText(
        "# React Testing",
      ),
    ).not.toBeInTheDocument();

    expect(
      mockedGetDocument,
    ).toHaveBeenCalledWith(
      "doc-1",
      expect.any(AbortSignal),
    );
  });

  it("loads a document, opens vectorization, vectorizes it, and updates its status", async () => {
    const user = userEvent.setup();

    mockedVectorizeDocument.mockResolvedValue({
      chunkCount: 3,
    } as any);

    // Keep focused vectorization status lookup behavior in the dialog.
    mockedGetVectorizationStatus.mockImplementation(
      async () => {
        if (
          mockedVectorizeDocument.mock.calls.length > 0
        ) {
          return vectorizedStatus as any;
        }

        return initialStatus as any;
      },
    );

    render(<FilesPage />);

    // ------------------------------------------------------------
    // 1. Initial document load
    // ------------------------------------------------------------

    const documentElement =
      await screen.findByText(
        "React Testing Guide.md",
      );

    expect(documentElement).toBeInTheDocument();

    // ------------------------------------------------------------
    // 2. Find document row
    // ------------------------------------------------------------

    const documentRow =
      documentElement.closest(
        ".document-list-item",
      );

    expect(documentRow).not.toBeNull();

    // ------------------------------------------------------------
    // 3. Confirm initial status
    // ------------------------------------------------------------

    expect(
      await within(
        documentRow as HTMLElement,
      ).findByText(
        "Not vectorized",
      ),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // 4. Open vectorization dialog
    // ------------------------------------------------------------

    const vectorizeButton =
      within(
        documentRow as HTMLElement,
      ).getByRole("button", {
        name: "Vectorize",
      });

    await user.click(vectorizeButton);

    // ------------------------------------------------------------
    // 5. Vectorization dialog opens
    // ------------------------------------------------------------

    const vectorizeDialog =
      await screen.findByRole("dialog", {
        name: /vectorize file/i,
      });

    expect(vectorizeDialog).toBeInTheDocument();

    expect(
      within(vectorizeDialog).getByText(
        "React Testing Guide.md",
      ),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // 6. Embedding model is loaded
    // ------------------------------------------------------------

    await waitFor(() => {
      expect(
        mockedListEmbeddingModels,
      ).toHaveBeenCalledWith(
        expect.any(AbortSignal),
      );
    });

    expect(
      within(vectorizeDialog).getByText(
        "Nomic Embed · nomic-embed-text · 768 dimensions · Ollama",
      ),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // 7. Default vectorization settings
    // ------------------------------------------------------------

    const spinButtons =
      within(vectorizeDialog).getAllByRole(
        "spinbutton",
      );

    expect(spinButtons).toHaveLength(2);

    expect(spinButtons[0]).toHaveValue("1000");
    expect(spinButtons[1]).toHaveValue("100");

    // ------------------------------------------------------------
    // 8. Confirm initial dialog state
    // ------------------------------------------------------------

    expect(
      within(vectorizeDialog).getByText(
        "Existing chunks",
      ),
    ).toBeInTheDocument();

    // The initial document has no chunks.
    expect(
      within(vectorizeDialog).getByText("0"),
    ).toBeInTheDocument();

    // ------------------------------------------------------------
    // 9. Submit vectorization
    // ------------------------------------------------------------

    const vectorizeSubmitButton =
      within(vectorizeDialog).getByRole(
        "button",
        {
          name: "Vectorize",
        },
      );

    expect(vectorizeSubmitButton).toBeEnabled();

    await user.click(vectorizeSubmitButton);

    /*
     * Ant Design changes the button's accessible name while
     * loading, e.g.:
     *
     *     loading Vectorize
     *
     * Therefore we do not query the button immediately after
     * clicking it.
     */

    // ------------------------------------------------------------
    // 10. Verify vectorization API request
    // ------------------------------------------------------------

    await waitFor(() => {
      expect(
        mockedVectorizeDocument,
      ).toHaveBeenCalledWith(
        "doc-1",
        {
          embeddingModelId: "model-1",
          maxChunkLength: 1000,
          chunkOverlap: 100,
        },
        expect.any(AbortSignal),
      );
    });

    // ------------------------------------------------------------
    // 11. The dialog performs its one-document status refresh after vectorization.
    // ------------------------------------------------------------

    await waitFor(() => {
      expect(
        mockedGetVectorizationStatus,
      ).toHaveBeenCalledWith(
        "doc-1",
        expect.any(AbortSignal),
      );
    });

    // ------------------------------------------------------------
    // 12. Success message appears
    // ------------------------------------------------------------

    expect(
        screen.getAllByText(
            "Vectorized successfully. Created 3 knowledge chunks.",
        ),
        ).not.toHaveLength(0);

    // ------------------------------------------------------------
    // 13. FilesPage receives the new status
    // ------------------------------------------------------------

    await waitFor(() => {
      expect(
        within(
          documentRow as HTMLElement,
        ).getByText(
          "Vectorized · 3 chunks",
        ),
      ).toBeInTheDocument();
    });

    // ------------------------------------------------------------
    // 14. Workspace success notice appears
    // ------------------------------------------------------------

    expect(
  screen.getAllByText(
    "Vectorized successfully. Created 3 knowledge chunks.",
  ),
).not.toHaveLength(0);

    // ------------------------------------------------------------
    // 15. Dialog changes into re-vectorization mode
    // ------------------------------------------------------------

    expect(
      within(vectorizeDialog).getByText(
        "Re-vectorize file",
      ),
    ).toBeInTheDocument();

    const reVectorizeButton =
  within(vectorizeDialog).getByRole(
    "button",
    {
      name: /Re-vectorize/,
    },
  );

    expect(reVectorizeButton).toBeEnabled();
  });
});
