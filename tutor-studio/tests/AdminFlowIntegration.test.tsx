import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import RuntimeEnginesPage from "../src/pages/admin/RuntimeEngines.jsx";
import ModelCatalogPage from "../src/pages/admin/EmbeddingModels.jsx";
import InteractionModelsPage from "../src/pages/admin/InteractionModels.jsx";

import { apiFetch } from "../src/api/client.js";

vi.mock("../src/api/client.js", () => ({
  apiFetch: vi.fn(),
  getApiErrorDescription: vi.fn(
    (error: unknown, fallback: string) =>
      error instanceof Error ? error.message : fallback,
  ),
}));

vi.mock("../src/hooks/useRolePermissions.js", () => ({
  default: () => ({
    canWrite: true,
    canDelete: true,
  }),
}));

const mockedApiFetch = vi.mocked(apiFetch);

const runtimeEngine = {
  id: "engine-1",
  name: "Local Ollama",
  providerType: 0,
  endpoint: "http://localhost:11434",
  apiKey: null,
  isEnabled: true,
};

const embeddingModel = {
  id: "embedding-1",
  name: "Local Embedding Model",
  runtimeEngineId: "engine-1",
  runtimeEngineName: "Local Ollama",
  providerModelName: "nomic-embed-text",
  embeddingDimensions: 768,
  settingsJson: null,
  isEnabled: true,
};

const interactionModel = {
  id: "interaction-1",
  name: "Local Interaction Model",
  runtimeEngineId: "engine-1",
  runtimeEngineName: "Local Ollama",
  providerModelName: "llama3.3",
  settingsJson: null,
  isEnabled: true,
};

function mockRuntimeEngineApi() {
  mockedApiFetch.mockImplementation(async (path, options = {}) => {
    if (
      path === "/api/admin/runtime-engines" &&
      (!options.method || options.method === "GET")
    ) {
      return [];
    }

    if (
      path === "/api/admin/runtime-engines" &&
      options.method === "POST"
    ) {
      return runtimeEngine;
    }

    if (
      path === "/api/admin/runtime-engines/test" &&
      options.method === "POST"
    ) {
      return {
        isSuccess: true,
        latencyMs: 15,
        message: "Connection verified.",
        availableModels: [
          "nomic-embed-text",
          "llama3.3",
          "bge-m3",
        ],
      };
    }

    if (
      path === "/api/admin/runtime-engines/engine-1" &&
      options.method === "DELETE"
    ) {
      return undefined;
    }

    throw new Error(
      `Unexpected apiFetch call: ${path} ${options.method ?? "GET"}`,
    );
  });
}

function mockEmbeddingModelApi() {
  mockedApiFetch.mockImplementation(async (path, options = {}) => {
    if (
      path === "/api/admin/embedding-models" &&
      (!options.method || options.method === "GET")
    ) {
      return [];
    }

    if (
      path === "/api/admin/runtime-engines" &&
      (!options.method || options.method === "GET")
    ) {
      return [runtimeEngine];
    }

    if (
      path === "/api/admin/runtime-engines/test" &&
      options.method === "POST"
    ) {
      return {
        isSuccess: true,
        latencyMs: 15,
        message: "Connection verified.",
        availableModels: [
          "nomic-embed-text",
          "llama3.3",
          "bge-m3",
        ],
      };
    }

    if (
      path === "/api/admin/embedding-models" &&
      options.method === "POST"
    ) {
      return embeddingModel;
    }

    throw new Error(
      `Unexpected apiFetch call: ${path} ${options.method ?? "GET"}`,
    );
  });
}

function mockInteractionModelApi() {
  mockedApiFetch.mockImplementation(async (path, options = {}) => {
    if (
      path === "/api/admin/interaction-models" &&
      (!options.method || options.method === "GET")
    ) {
      return [];
    }

    if (
      path === "/api/admin/runtime-engines" &&
      (!options.method || options.method === "GET")
    ) {
      return [runtimeEngine];
    }

    if (
      path === "/api/admin/runtime-engines/test" &&
      options.method === "POST"
    ) {
      return {
        isSuccess: true,
        latencyMs: 15,
        message: "Connection verified.",
        availableModels: [
          "nomic-embed-text",
          "llama3.3",
          "bge-m3",
        ],
      };
    }

    if (
      path === "/api/admin/interaction-models" &&
      options.method === "POST"
    ) {
      return interactionModel;
    }

    throw new Error(
      `Unexpected apiFetch call: ${path} ${options.method ?? "GET"}`,
    );
  });
}

async function selectRuntimeEngine(
  user: ReturnType<typeof userEvent.setup>,
) {
  const combobox = screen.getByRole("combobox", {
    name: /runtime engine/i,
  });

  await user.click(combobox);

  await waitFor(() => {
    expect(
      screen.getAllByText("Local Ollama").length,
    ).toBeGreaterThanOrEqual(2);
  });

  const option = screen.getByRole("option", {
    name: "Local Ollama",
  });

  await user.click(option);
}

describe("Admin integration workflow", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("creates a runtime engine and makes it available to the model catalog", async () => {
    const user = userEvent.setup();

    mockRuntimeEngineApi();

    render(<RuntimeEnginesPage />);

    await waitFor(() => {
      expect(
        screen.getByText("No runtime engines found"),
      ).toBeInTheDocument();
    });

    await user.click(
      screen.getByRole("button", {
        name: /add engine/i,
      }),
    );

    expect(
      screen.getByRole("dialog", {
        name: /add runtime engine/i,
      }),
    ).toBeInTheDocument();

    const nameInput = screen.getByRole("textbox", {
      name: "Name",
    });

    await user.type(nameInput, "Local Ollama");

    const endpointInput = screen.getByRole("textbox", {
      name: "Endpoint",
    });

    await user.clear(endpointInput);
    await user.type(
      endpointInput,
      "http://localhost:11434",
    );

    await user.click(
      screen.getByRole("button", {
        name: /save engine/i,
      }),
    );

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/runtime-engines",
        expect.objectContaining({
          method: "POST",
          json: expect.objectContaining({
            name: "Local Ollama",
            endpoint: "http://localhost:11434",
            isEnabled: true,
          }),
        }),
      );
    });

    await waitFor(() => {
      expect(
        screen.getByText("Local Ollama"),
      ).toBeInTheDocument();
    });
  });

  it("uses a runtime engine to discover provider models and create an embedding model", async () => {
    const user = userEvent.setup();

    mockEmbeddingModelApi();

    render(<ModelCatalogPage />);

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/embedding-models",
      );
    });

    await waitFor(() => {
      expect(
        screen.getByRole("button", {
          name: /add model/i,
        }),
      ).toBeInTheDocument();
    });

    await user.click(
      screen.getByRole("button", {
        name: /add model/i,
      }),
    );

    expect(
      screen.getByRole("dialog", {
        name: /add embedding model/i,
      }),
    ).toBeInTheDocument();

    await selectRuntimeEngine(user);

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/runtime-engines/test",
        expect.objectContaining({
          method: "POST",
          json: expect.objectContaining({
            providerType: 0,
            endpoint: "http://localhost:11434",
            runtimeEngineId: "engine-1",
          }),
        }),
      );
    });

    await waitFor(() => {
      expect(
        screen.getByText("nomic-embed-text (768d)"),
      ).toBeInTheDocument();
    });

    await user.click(
      screen.getByText("nomic-embed-text (768d)"),
    );

    /*
     * IMPORTANT:
     * Use the exact "Name" accessible label here.
     *
     * Using /name/i also matches "Provider model name",
     * which is why the previous test failed.
     */
    const nameInput = screen.getByRole("textbox", {
      name: "Name",
    });

    await user.type(
      nameInput,
      "Local Embedding Model",
    );

    await waitFor(() => {
      const providerModelInput =
        screen.getByRole("textbox", {
          name: /provider model name/i,
        });

      expect(providerModelInput).toHaveValue(
        "nomic-embed-text",
      );
    });

    const dimensionInput = screen.getByRole(
      "spinbutton",
      {
        name: /embedding dimensions/i,
      },
    );

    expect(dimensionInput).toHaveValue("768");

    await user.click(
      screen.getByRole("button", {
        name: /save model/i,
      }),
    );

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/embedding-models",
        expect.objectContaining({
          method: "POST",
          json: expect.objectContaining({
            name: "Local Embedding Model",
            runtimeEngineId: "engine-1",
            providerModelName: "nomic-embed-text",
            embeddingDimensions: 768,
            isEnabled: true,
          }),
        }),
      );
    });

    expect(
      mockedApiFetch.mock.calls.some(
        ([path, options]) =>
          path ===
            "/api/admin/runtime-engines/test" &&
          options?.method === "POST",
      ),
    ).toBe(true);

    expect(
      mockedApiFetch.mock.calls.some(
        ([path, options]) =>
          path ===
            "/api/admin/embedding-models" &&
          options?.method === "POST",
      ),
    ).toBe(true);
  });

  it("uses the same runtime engine to create an interaction model", async () => {
    const user = userEvent.setup();

    mockInteractionModelApi();

    render(<InteractionModelsPage />);

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/interaction-models",
      );
    });

    await waitFor(() => {
      expect(
        screen.getByRole("button", {
          name: /add model/i,
        }),
      ).toBeInTheDocument();
    });

    await user.click(
      screen.getByRole("button", {
        name: /add model/i,
      }),
    );

    expect(
      screen.getByRole("dialog", {
        name: /add interaction model/i,
      }),
    ).toBeInTheDocument();

    await selectRuntimeEngine(user);

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/runtime-engines/test",
        expect.objectContaining({
          method: "POST",
          json: expect.objectContaining({
            providerType: 0,
            endpoint: "http://localhost:11434",
            runtimeEngineId: "engine-1",
          }),
        }),
      );
    });

    await waitFor(() => {
      expect(
        screen.getByText("llama3.3"),
      ).toBeInTheDocument();
    });

    await user.click(
      screen.getByText("llama3.3"),
    );

    /*
     * Use the exact "Name" label instead of /name/i.
     * Otherwise "Provider model name" also matches.
     */
    const nameInput = screen.getByRole("textbox", {
      name: "Name",
    });

    await user.type(
      nameInput,
      "Local Interaction Model",
    );

    await waitFor(() => {
      const providerModelInput =
        screen.getByRole("textbox", {
          name: /provider model name/i,
        });

      expect(providerModelInput).toHaveValue(
        "llama3.3",
      );
    });

    await user.click(
      screen.getByRole("button", {
        name: /save model/i,
      }),
    );

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/interaction-models",
        expect.objectContaining({
          method: "POST",
          json: expect.objectContaining({
            name: "Local Interaction Model",
            runtimeEngineId: "engine-1",
            providerModelName: "llama3.3",
            isEnabled: true,
          }),
        }),
      );
    });

    expect(
      mockedApiFetch.mock.calls.some(
        ([path, options]) =>
          path ===
            "/api/admin/runtime-engines/test" &&
          options?.method === "POST",
      ),
    ).toBe(true);

    expect(
      mockedApiFetch.mock.calls.some(
        ([path, options]) =>
          path ===
            "/api/admin/interaction-models" &&
          options?.method === "POST",
      ),
    ).toBe(true);
  });

  it("shows Configured tag and allows clearing stored API key on edit", async () => {
    const user = userEvent.setup();

    const keyedEngine = {
      id: "engine-keyed",
      name: "Keyed OpenAI",
      providerType: 1,
      endpoint: "https://api.openai.com/v1",
      hasApiKey: true,
      isEnabled: true,
    };

    mockedApiFetch.mockImplementation(async (path, options = {}) => {
      if (
        path === "/api/admin/runtime-engines" &&
        (!options.method || options.method === "GET")
      ) {
        return [keyedEngine];
      }

      if (
        path === "/api/admin/runtime-engines/engine-keyed" &&
        options.method === "PUT"
      ) {
        return {
          ...keyedEngine,
          hasApiKey: false,
        };
      }

      throw new Error(
        `Unexpected apiFetch call: ${path} ${options.method ?? "GET"}`,
      );
    });

    render(<RuntimeEnginesPage />);

    await waitFor(() => {
      expect(screen.getByText("Keyed OpenAI")).toBeInTheDocument();
      expect(screen.getByText("Configured")).toBeInTheDocument();
    });

    const editBtn = screen.getByRole("button", { name: "Edit engine" });
    await user.click(editBtn);

    await waitFor(() => {
      expect(
        screen.getByRole("dialog"),
      ).toBeInTheDocument();
    });

    const dialog = screen.getByRole("dialog");
    expect(dialog).toHaveTextContent("Configured");

    // Verify testing connection in edit mode sends runtimeEngineId for server-side key prefill
    mockedApiFetch.mockImplementation(async (path, options = {}) => {
      if (
        path === "/api/admin/runtime-engines/test" &&
        options.method === "POST"
      ) {
        return {
          isSuccess: true,
          latencyMs: 12,
          message: "Connection verified via prefill.",
          availableModels: ["gpt-4o"],
        };
      }
      if (
        path === "/api/admin/runtime-engines/engine-keyed" &&
        options.method === "PUT"
      ) {
        return {
          ...keyedEngine,
          hasApiKey: false,
        };
      }
      throw new Error(`Unexpected apiFetch call: ${path} ${options.method ?? "GET"}`);
    });

    const testBtn = screen.getByRole("button", { name: /test connection/i });
    await user.click(testBtn);

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/runtime-engines/test",
        expect.objectContaining({
          method: "POST",
          json: expect.objectContaining({
            providerType: 1,
            endpoint: "https://api.openai.com/v1",
            apiKey: null,
            runtimeEngineId: "engine-keyed",
          }),
        }),
      );
    });

    const clearSwitch = screen.getByRole("switch", {
      name: /clear stored key/i,
    });
    expect(clearSwitch).toBeInTheDocument();
    expect(clearSwitch).not.toBeChecked();

    await user.click(clearSwitch);
    expect(clearSwitch).toBeChecked();

    await user.click(
      screen.getByRole("button", {
        name: /save engine/i,
      }),
    );

    await waitFor(() => {
      expect(mockedApiFetch).toHaveBeenCalledWith(
        "/api/admin/runtime-engines/engine-keyed",
        expect.objectContaining({
          method: "PUT",
          json: expect.objectContaining({
            name: "Keyed OpenAI",
            providerType: 1,
            endpoint: "https://api.openai.com/v1",
            clearApiKey: true,
            isEnabled: true,
          }),
        }),
      );
    });
  });
});
