import { describe, expect, it, beforeEach, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import VectorizeDocumentDialog from "../src/features/editor/components/VectorizeDocumentDialog.jsx";

import {
  getVectorizationStatus,
  listEmbeddingModels,
  vectorizeDocument,
} from "../src/features/editor/utils/documentApi.js";

import { getApiErrorDescription } from "../src/api/client.js";

vi.mock("../src/features/editor/utils/documentApi.js", () => ({
  getVectorizationStatus: vi.fn(),
  listEmbeddingModels: vi.fn(),
  vectorizeDocument: vi.fn(),
}));

vi.mock("../src/api/client.js", () => ({
  getApiErrorDescription: vi.fn(
    (_error, fallback) => fallback
  ),
}));

const documentMock = {
  id: "document-123",
  name: "Test Document.pdf",
};

const modelsMock = [
  {
    id: "model-1",
    name: "Nomic Embed",
    providerModelName: "nomic-embed-text",
    embeddingDimensions: 768,
    runtimeEngineName: "Ollama",
    isEnabled: true,
    isRuntimeEngineEnabled: true,
  },
  {
    id: "model-2",
    name: "Disabled Model",
    providerModelName: "disabled-model",
    embeddingDimensions: 384,
    runtimeEngineName: "Ollama",
    isEnabled: false,
    isRuntimeEngineEnabled: true,
  },
  {
    id: "model-3",
    name: "Disabled Runtime",
    providerModelName: "runtime-disabled",
    embeddingDimensions: 768,
    runtimeEngineName: "Ollama",
    isEnabled: true,
    isRuntimeEngineEnabled: false,
  },
];

const statusMock = {
  hasRawText: true,
  isVectorized: false,
  embeddingModelId: "model-1",
  chunkCount: 0,
  lastVectorizedUtc: null,
};

function getModal() {
  return screen.getByRole("dialog");
}

function renderDialog(props = {}) {
  return render(
    <VectorizeDocumentDialog
      document={documentMock}
      onClose={vi.fn()}
      onVectorized={vi.fn()}
      {...props}
    />
  );
}

describe("VectorizeDocumentDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    getVectorizationStatus.mockResolvedValue(statusMock);
    listEmbeddingModels.mockResolvedValue(modelsMock);
    vectorizeDocument.mockResolvedValue({
      chunkCount: 12,
    });
  });

  it("renders the document name and loads vectorization settings", async () => {
    renderDialog();

    expect(
      screen.getByText("Test Document.pdf")
    ).toBeInTheDocument();

    expect(
      screen.getByText("Loading vectorization settings")
    ).toBeInTheDocument();

    await waitFor(() => {
      expect(
        screen.getByText("Embedding model")
      ).toBeInTheDocument();
    });

    expect(getVectorizationStatus).toHaveBeenCalledWith(
      "document-123",
      expect.any(AbortSignal)
    );

    expect(listEmbeddingModels).toHaveBeenCalledWith(
      expect.any(AbortSignal)
    );
  });

  it("only displays enabled models with an enabled runtime engine", async () => {
    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Embedding model")).toBeInTheDocument();
    });

    expect(
      screen.getByText(
        "Nomic Embed Â· nomic-embed-text Â· 768 dimensions Â· Ollama"
      )
    ).toBeInTheDocument();

    expect(
      screen.queryByText(
        "Disabled Model Â· disabled-model Â· 384 dimensions Â· Ollama"
      )
    ).not.toBeInTheDocument();

    expect(
      screen.queryByText(
        "Disabled Runtime Â· runtime-disabled Â· 768 dimensions Â· Ollama"
      )
    ).not.toBeInTheDocument();
  });

  it("uses the existing embedding model when it is available", async () => {
    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Embedding model")).toBeInTheDocument();
    });

    expect(
  await screen.findByTitle(
    "Nomic Embed Â· nomic-embed-text Â· 768 dimensions Â· Ollama"
  )
).toBeInTheDocument();
  });

  it("falls back to the first usable model when the existing model is unavailable", async () => {
    getVectorizationStatus.mockResolvedValue({
      ...statusMock,
      embeddingModelId: "missing-model",
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Embedding model")).toBeInTheDocument();
    });

    expect(
  await screen.findByTitle(
    "Nomic Embed Â· nomic-embed-text Â· 768 dimensions Â· Ollama"
  )
).toBeInTheDocument();
  });

  it("displays default chunk size and overlap", async () => {
    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Chunk size")).toBeInTheDocument();
    });

    const inputs = screen.getAllByRole("spinbutton");

    expect(inputs).toHaveLength(2);
    expect(inputs[0]).toHaveValue("1000");
    expect(inputs[1]).toHaveValue("100");
  });

  it("displays existing chunk count and last vectorized date", async () => {
    getVectorizationStatus.mockResolvedValue({
      ...statusMock,
      chunkCount: 25,
      lastVectorizedUtc: "2026-09-01T10:30:00Z",
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Existing chunks")).toBeInTheDocument();
    });

    expect(screen.getByText("25")).toBeInTheDocument();

    expect(
      screen.getByText(
        new Date("2026-09-01T10:30:00Z").toLocaleString()
      )
    ).toBeInTheDocument();
  });

  it("shows a warning when the document has no extracted text", async () => {
    getVectorizationStatus.mockResolvedValue({
      ...statusMock,
      hasRawText: false,
    });

    renderDialog();

    await waitFor(() => {
      expect(
        screen.getByText(
          "This document has no extracted text. Add file content before vectorizing it."
        )
      ).toBeInTheDocument();
    });
  });

  it("disables vectorization when the document has no raw text", async () => {
    getVectorizationStatus.mockResolvedValue({
      ...statusMock,
      hasRawText: false,
    });

    renderDialog();

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "Vectorize" })
      ).toBeDisabled();
    });
  });

  it("shows a warning when there are no usable embedding models", async () => {
    listEmbeddingModels.mockResolvedValue([]);

    renderDialog();

    await waitFor(() => {
      expect(
        screen.getByText("No enabled embedding model is available")
      ).toBeInTheDocument();
    });

    expect(
      screen.getByText(
        "Ask an administrator to configure an embedding model and ensure its runtime model is available."
      )
    ).toBeInTheDocument();
  });

  it("shows re-vectorize mode when the document is already vectorized", async () => {
    getVectorizationStatus.mockResolvedValue({
      ...statusMock,
      isVectorized: true,
      chunkCount: 15,
    });

    renderDialog();

    await waitFor(() => {
      expect(
        screen.getByText("Re-vectorize file")
      ).toBeInTheDocument();
    });

    expect(
      screen.getByRole("button", { name: "Re-vectorize" })
    ).toBeInTheDocument();

    expect(
      screen.getByText(
        "This replaces 15 existing chunks after every new embedding has been generated successfully."
      )
    ).toBeInTheDocument();
  });

  it("validates chunk size when it is below the minimum", async () => {
    const user = userEvent.setup();

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Chunk size")).toBeInTheDocument();
    });

    const inputs = screen.getAllByRole("spinbutton");

    await user.clear(inputs[0]);
    await user.type(inputs[0], "50");

    expect(
      screen.getByText(
        "Chunk size must be a whole number from 100 to 8000."
      )
    ).toBeInTheDocument();

    expect(
      screen.getByRole("button", { name: "Vectorize" })
    ).toBeDisabled();
  });
  it("validates that overlap is less than chunk size", async () => {
    const user = userEvent.setup();

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Chunk overlap")).toBeInTheDocument();
    });

    const inputs = screen.getAllByRole("spinbutton");

    await user.clear(inputs[1]);
    await user.type(inputs[1], "1000");

    expect(
      screen.getByText(
        "Chunk overlap must be a whole number from 0 up to one less than chunk size."
      )
    ).toBeInTheDocument();

    expect(
      screen.getByRole("button", { name: "Vectorize" })
    ).toBeDisabled();
  });

  it("successfully vectorizes the document", async () => {
    const user = userEvent.setup();
    const onVectorized = vi.fn();

    const refreshedStatus = {
      ...statusMock,
      isVectorized: true,
      chunkCount: 12,
    };

    getVectorizationStatus
      .mockResolvedValueOnce(statusMock)
      .mockResolvedValueOnce(refreshedStatus);

    renderDialog({ onVectorized });

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "Vectorize" })
      ).toBeEnabled();
    });

    await user.click(
      screen.getByRole("button", { name: "Vectorize" })
    );

    await waitFor(() => {
      expect(vectorizeDocument).toHaveBeenCalledWith(
        "document-123",
        {
          embeddingModelId: "model-1",
          maxChunkLength: 1000,
          chunkOverlap: 100,
        },
        expect.any(AbortSignal)
      );
    });

    expect(getVectorizationStatus).toHaveBeenCalledTimes(2);

    await waitFor(() => {
      expect(
        screen.getByText(
          "Vectorized successfully. Created 12 knowledge chunks."
        )
      ).toBeInTheDocument();
    });

    expect(onVectorized).toHaveBeenCalledWith(
      "document-123",
      refreshedStatus
    );
  });

  it("shows the API error when vectorization fails", async () => {
    const user = userEvent.setup();

    vectorizeDocument.mockRejectedValue(
      new Error("Embedding service unavailable")
    );

    renderDialog();

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "Vectorize" })
      ).toBeEnabled();
    });

    await user.click(
      screen.getByRole("button", { name: "Vectorize" })
    );

    await waitFor(() => {
      expect(
        screen.getByText(
          "The embedding service could not process this document. Confirm the model is installed and the runtime is available."
        )
      ).toBeInTheDocument();
    });

    expect(getApiErrorDescription).toHaveBeenCalled();
  });

  it("does not vectorize when there is no model selected", async () => {
    listEmbeddingModels.mockResolvedValue([]);

    renderDialog();

    await waitFor(() => {
      expect(
        screen.getByText("No enabled embedding model is available")
      ).toBeInTheDocument();
    });

    expect(vectorizeDocument).not.toHaveBeenCalled();
  });

  it("calls onClose when the modal is cancelled", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();

    renderDialog({ onClose });

    await waitFor(() => {
      expect(
        screen.getByRole("button", { name: "Cancel" })
      ).toBeInTheDocument();
    });

    await user.click(
      screen.getByRole("button", { name: "Cancel" })
    );

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
