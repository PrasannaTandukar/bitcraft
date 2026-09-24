import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ModelCatalogManager from "../src/features/admin/components/ModelCatalogManager.jsx";

vi.mock("../src/hooks/useRolePermissions.js", () => ({
  __esModule: true,
  default: vi.fn(),
}));

vi.mock("../../../components/PageHeader.jsx", () => ({
  __esModule: true,
  default: ({ title, description, extra }: any) => (
    <div data-testid="page-header">
      <h1>{title}</h1>
      <p>{description}</p>
      <div data-testid="page-header-extra">{extra}</div>
    </div>
  ),
}));

vi.mock("../src/features/admin/hooks/useModelCatalog.js", () => ({
  __esModule: true,
  default: vi.fn(),
}));

vi.mock("../src/features/admin/utils/modelCatalogUtils.js", () => ({
  __esModule: true,
  matchEmbeddingDimension: vi.fn(() => undefined),
}));

import useRolePermissions from "../src/hooks/useRolePermissions.js";
import useModelCatalog from "../src/features/admin/hooks/useModelCatalog.js";
import { matchEmbeddingDimension } from "../src/features/admin/utils/modelCatalogUtils.js";
// vi.mocked() gives back a properly-typed mock and, more importantly here,
// is the reliable way to reach the vi.fn()
const mockUseRolePermissions = vi.mocked(useRolePermissions);
const mockUseModelCatalog = vi.mocked(useModelCatalog);
const mockMatchEmbeddingDimension = vi.mocked(matchEmbeddingDimension);

const baseConfig = {
  title: "Interaction Models",
  description: "Manage models used for tutor interactions.",
  singular: "model",
  providerPlaceholder: "gpt-4o-mini",
};

const sampleModels = [
  {
    id: "1",
    name: "Fast Chat",
    providerModelName: "gpt-4o-mini",
    runtimeEngineName: "OpenAI Engine",
    isEnabled: true,
    embeddingDimensions: 0,
  },
  {
    id: "2",
    name: "Slow Chat",
    providerModelName: "gpt-4o",
    runtimeEngineName: "OpenAI Engine",
    isEnabled: false,
    embeddingDimensions: 0,
  },
];

const sampleEngines = [
  { id: "engine-1", name: "OpenAI Engine" },
  { id: "engine-2", name: "Azure Engine" },
];

function makeCatalogState(overrides: Record<string, any> = {}) {
  return {
    config: baseConfig,
    isEmbedding: false,
    engines: sampleEngines,
    loading: false,
    refreshing: false,
    error: null,
    engineError: null,
    open: false,
    setOpen: vi.fn(),
    editingModel: null,
    saving: false,
    formError: null,
    search: "",
    setSearch: vi.fn(),
    deletingId: null,
    discoveredModelsByEngine: {},
    fetchingEngineModels: false,
    engineProbeError: null,
    filteredModels: sampleModels,
    load: vi.fn(),
    loadEngineModels: vi.fn(),
    openCreate: vi.fn(),
    openEdit: vi.fn(),
    close: vi.fn(),
    save: vi.fn(),
    remove: vi.fn(),
    ...overrides,
  };
}

function setup({
  permissions = { canWrite: true, canDelete: true },
  catalog = {},
}: { permissions?: { canWrite: boolean; canDelete: boolean }; catalog?: Record<string, any> } = {}) {
  mockUseRolePermissions.mockReturnValue(permissions);
  const state = makeCatalogState(catalog);
  mockUseModelCatalog.mockReturnValue(state);
  const utils = render(<ModelCatalogManager kind="interaction" />);
  return { ...utils, state };
}

beforeEach(() => {
  vi.clearAllMocks();
});

// rc-select (which AntD's Select wraps) keeps a hidden, 0x0 "accessibility
// mirror" list in the DOM at all times (role="listbox", but its visible
// textContent is the raw option *value*, e.g. "engine-1", not the label).
// The actual clickable dropdown the user sees is portaled separately as
// `.ant-select-dropdown` and does NOT carry role="listbox" itself. We must
// scope to that real popup, or queries either match the wrong (hidden) node
// or throw "unable to find text" because the visible text there is the id.
async function selectRuntimeEngine(user: ReturnType<typeof userEvent.setup>, engineName: string) {
  await user.click(screen.getByLabelText("Runtime engine"));
  const dropdown = await waitFor(() => {
    const el = document.querySelector(".ant-select-dropdown:not(.ant-select-dropdown-hidden)");
    if (!el) throw new Error("Select dropdown is not open yet");
    return el as HTMLElement;
  });
  const option = within(dropdown).getByText(engineName);
  await user.click(option);
}

// The Drawer's form fields and title text can collide with identically-named
// elements elsewhere on the page (e.g. the sortable "Name" table column
// header also carries aria-label="Name"; the header's "Add model" button and
// the drawer's title both render the literal text "Add model"). Scoping to
// the drawer's dialog role avoids all of that ambiguity.
function drawer() {
  return within(screen.getByRole("dialog"));
}

// ---------------------------------------------------------------------------
// Rendering / basic content
// ---------------------------------------------------------------------------

describe("ModelCatalogManager - rendering", () => {
  it("renders the page title and description from config", () => {
    setup();
    expect(screen.getByText("Interaction Models")).toBeInTheDocument();
    expect(screen.getByText("Manage models used for tutor interactions.")).toBeInTheDocument();
  });

  it("renders a row for each model in the table", () => {
    setup();
    expect(screen.getByText("Fast Chat")).toBeInTheDocument();
    expect(screen.getByText("Slow Chat")).toBeInTheDocument();
    expect(screen.getByText("gpt-4o-mini")).toBeInTheDocument();
    expect(screen.getByText("gpt-4o")).toBeInTheDocument();
  });

  it("shows an empty state when there are no models", () => {
    setup({ catalog: { filteredModels: [] } });
    expect(screen.getByText(/No interaction models found/i)).toBeInTheDocument();
  });

  it("shows an error alert when error is set", () => {
    setup({ catalog: { error: "Failed to load models" } });
    expect(screen.getByText("Failed to load models")).toBeInTheDocument();
  });

  it("renders provider model fallback text when providerModelName is missing", () => {
    setup({
      catalog: {
        filteredModels: [
          { id: "3", name: "No Provider", providerModelName: "", runtimeEngineName: "Engine", isEnabled: true, embeddingDimensions: 0 },
        ],
      },
    });
    expect(screen.getByText("Provider model not specified")).toBeInTheDocument();
  });

  it("renders an em dash when runtimeEngineName is missing", () => {
    setup({
      catalog: {
        filteredModels: [
          { id: "4", name: "No Engine", providerModelName: "gpt", runtimeEngineName: "", isEnabled: true, embeddingDimensions: 0 },
        ],
      },
    });
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });

  it("shows the Dimensions column only for embedding models", () => {
    const { rerender } = setup({ catalog: { isEmbedding: false } });
    expect(screen.queryByText("Dimensions")).not.toBeInTheDocument();

    mockUseRolePermissions.mockReturnValue({ canWrite: true, canDelete: true });
    mockUseModelCatalog.mockReturnValue(
      makeCatalogState({
        isEmbedding: true,
        filteredModels: [
          { id: "5", name: "Embed Model", providerModelName: "text-embed-3", runtimeEngineName: "Engine", isEnabled: true, embeddingDimensions: 1536 },
        ],
      })
    );
    rerender(<ModelCatalogManager kind="embedding" />);
    expect(screen.getByText("Dimensions")).toBeInTheDocument();
    expect(screen.getByText("1,536")).toBeInTheDocument();
  });

  it("renders an em dash for zero/absent embedding dimensions", () => {
    setup({
      catalog: {
        isEmbedding: true,
        filteredModels: [
          { id: "6", name: "Embed Model", providerModelName: "text-embed-3", runtimeEngineName: "Engine", isEnabled: true, embeddingDimensions: 0 },
        ],
      },
    });
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });
});

// ---------------------------------------------------------------------------
// Permissions
// ---------------------------------------------------------------------------

describe("ModelCatalogManager - permissions", () => {
  it("shows the Add model button when canWrite is true", () => {
    setup({ permissions: { canWrite: true, canDelete: true } });
    expect(screen.getByRole("button", { name: /add model/i })).toBeInTheDocument();
  });

  it("hides the Add model button when canWrite is false", () => {
    setup({ permissions: { canWrite: false, canDelete: false } });
    expect(screen.queryByRole("button", { name: /add model/i })).not.toBeInTheDocument();
  });

  it("shows edit and delete buttons per row when permitted", () => {
    setup({ permissions: { canWrite: true, canDelete: true } });
    expect(screen.getAllByRole("button", { name: /^Edit model$/i })).toHaveLength(2);
    expect(screen.getAllByRole("button", { name: /^Delete model$/i })).toHaveLength(2);
  });

  it("hides edit buttons when canWrite is false", () => {
    setup({ permissions: { canWrite: false, canDelete: true } });
    expect(screen.queryByRole("button", { name: /^Edit model$/i })).not.toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: /^Delete model$/i })).toHaveLength(2);
  });

  it("hides delete buttons when canDelete is false", () => {
    setup({ permissions: { canWrite: true, canDelete: false } });
    expect(screen.queryByRole("button", { name: /^Delete model$/i })).not.toBeInTheDocument();
  });

  it("hides the action column entirely when neither canWrite nor canDelete", () => {
    setup({ permissions: { canWrite: false, canDelete: false } });
    expect(screen.queryByRole("button", { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Interactions: search, refresh, create, edit, delete
// ---------------------------------------------------------------------------

describe("ModelCatalogManager - interactions", () => {
  it("calls setSearch when typing into the search box", async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const searchInput = screen.getByPlaceholderText("Search models");
    await user.type(searchInput, "fast");
    expect(state.setSearch).toHaveBeenCalled();
  });

  it("calls load(true) when clicking Refresh", async () => {
    const user = userEvent.setup();
    const { state } = setup();
    await user.click(screen.getByRole("button", { name: /refresh/i }));
    expect(state.load).toHaveBeenCalledWith(true);
  });

  it("calls openCreate when clicking Add model", async () => {
    const user = userEvent.setup();
    const { state } = setup();
    await user.click(screen.getByRole("button", { name: /add model/i }));
    expect(state.openCreate).toHaveBeenCalledTimes(1);
  });

  it("calls openEdit with the correct model when clicking its Edit button", async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const editButtons = screen.getAllByRole("button", { name: /^Edit model$/i });
    await user.click(editButtons[0]);
    expect(state.openEdit).toHaveBeenCalledWith(sampleModels[0]);
  });

  it("calls remove with the correct model after confirming deletion", async () => {
    const user = userEvent.setup();
    const { state } = setup();
    const deleteButtons = screen.getAllByRole("button", { name: /^Delete model$/i });
    await user.click(deleteButtons[1]);

    // Popconfirm renders a confirmation popup with a "Delete" button
    const confirmButton = await screen.findByRole("button", { name: "Delete" });
    await user.click(confirmButton);

    expect(state.remove).toHaveBeenCalledWith(sampleModels[1]);
  });

  it("shows a loading spinner on the delete button matching deletingId", () => {
    setup({ catalog: { deletingId: "1" } });
    const deleteButtons = screen.getAllByRole("button", { name: /^Delete model$/i });
    expect(deleteButtons[0].querySelector(".anticon-loading")).toBeTruthy();
  });

  it("filters rows client-side via the Status column filter", async () => {
    const user = userEvent.setup();
    setup();
    // Ant Design filter dropdown trigger button
    const filterTrigger = document.querySelector(".ant-table-filter-trigger") as HTMLElement;
    expect(filterTrigger).toBeTruthy();
    await user.click(filterTrigger);

    // "Enabled" also appears as a Status tag on the Fast Chat row, so scope
    // to the filter dropdown popup rather than querying the whole document.
    const filterDropdown = await waitFor(() => {
      const el = document.querySelector(".ant-table-filter-dropdown");
      if (!el) throw new Error("Filter dropdown is not open yet");
      return el as HTMLElement;
    });
    const enabledOption = within(filterDropdown).getByText("Enabled");
    await user.click(enabledOption);

    const okButton = screen.getByRole("button", { name: /^ok$/i });
    await user.click(okButton);

    await waitFor(() => {
      expect(screen.getByText("Fast Chat")).toBeInTheDocument();
      expect(screen.queryByText("Slow Chat")).not.toBeInTheDocument();
    });
  });
});

// ---------------------------------------------------------------------------
// Drawer / form behavior
// ---------------------------------------------------------------------------

describe("ModelCatalogManager - drawer form", () => {
  it("does not render the drawer content when open is false", () => {
    setup({ catalog: { open: false } });
    // The Drawer uses destroyOnHidden, so with it closed there should be no
    // dialog in the document at all. (Querying for the "Name" field directly
    // is ambiguous: the sortable table column header also carries
    // aria-label="Name".)
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("renders the create-mode drawer with expected fields", () => {
    setup({ catalog: { open: true, editingModel: null } });
    expect(drawer().getByText(/^Add model$/)).toBeInTheDocument();
    expect(drawer().getByLabelText("Name")).toBeInTheDocument();
    expect(drawer().getByLabelText("Runtime engine")).toBeInTheDocument();
    expect(drawer().getByLabelText("Provider model name")).toBeInTheDocument();
    expect(drawer().getByLabelText("Settings JSON")).toBeInTheDocument();
    expect(drawer().getByLabelText("Enabled")).toBeInTheDocument();
  });

  it("renders the edit-mode drawer title when editingModel is set", () => {
    setup({ catalog: { open: true, editingModel: sampleModels[0] } });
    expect(drawer().getByText(/^Edit model$/)).toBeInTheDocument();
  });

  it("shows a warning when there are no engines and not editing", () => {
    setup({ catalog: { open: true, editingModel: null, engines: [] } });
    expect(screen.getByText(/No enabled runtime engines are available/i)).toBeInTheDocument();
  });

  it("disables Save when there are no engines and not editing", () => {
    setup({ catalog: { open: true, editingModel: null, engines: [] } });
    expect(screen.getByRole("button", { name: /save model/i })).toBeDisabled();
  });

  it("calls close when clicking Cancel", async () => {
    const user = userEvent.setup();
    const { state } = setup({ catalog: { open: true } });
    await user.click(screen.getByRole("button", { name: /cancel/i }));
    expect(state.close).toHaveBeenCalledTimes(1);
  });

  it("submits the form via save() when clicking Save model", async () => {
    const user = userEvent.setup();
    const { state } = setup({ catalog: { open: true } });
    await user.type(drawer().getByLabelText("Name"), "My New Model");
    // Runtime engine is also a required field — without selecting one, Form
    // validation blocks onFinish and save() never gets called.
    await selectRuntimeEngine(user, "OpenAI Engine");
    await user.click(screen.getByRole("button", { name: /save model/i }));
    await waitFor(() => expect(state.save).toHaveBeenCalled());
  });

  it("shows a validation error when Name is left blank", async () => {
    const user = userEvent.setup();
    setup({ catalog: { open: true } });
    await user.click(screen.getByRole("button", { name: /save model/i }));
    expect(await screen.findByText("Enter a model name.")).toBeInTheDocument();
  });

  it("shows the formError alert when present", () => {
    setup({ catalog: { open: true, formError: "Something went wrong saving." } });
    expect(screen.getByText("Something went wrong saving.")).toBeInTheDocument();
  });

  it("shows the engineError alert when present", () => {
    setup({ catalog: { open: true, engineError: "Could not reach runtime engines." } });
    expect(screen.getByText("Could not reach runtime engines.")).toBeInTheDocument();
  });

  it("calls loadEngineModels when selecting a runtime engine", async () => {
    const user = userEvent.setup();
    const { state } = setup({ catalog: { open: true } });
    await selectRuntimeEngine(user, "OpenAI Engine");
    expect(state.loadEngineModels).toHaveBeenCalledWith("engine-1", true);
  });

  it("shows the Embedding dimensions field for embedding models", () => {
    setup({ catalog: { open: true, isEmbedding: true } });
    expect(screen.getByLabelText("Embedding dimensions")).toBeInTheDocument();
  });

  it("disables Embedding dimensions when editing an existing model", () => {
    setup({
      catalog: {
        open: true,
        isEmbedding: true,
        editingModel: {
          id: "5",
          name: "Embed Model",
          providerModelName: "text-embed-3",
          runtimeEngineId: "engine-1",
          isEnabled: true,
          embeddingDimensions: 1536,
        },
      },
    });
    expect(screen.getByLabelText("Embedding dimensions")).toBeDisabled();
  });

  it("shows a message when no provider models have been discovered yet", () => {
    setup({ catalog: { open: true, discoveredModelsByEngine: {} } });
    expect(screen.getByText(/Select a runtime engine above to load available provider models/i)).toBeInTheDocument();
  });

  it("shows fetching state while provider models are loading", async () => {
    const user = userEvent.setup();
    setup({ catalog: { open: true, fetchingEngineModels: true } });
    await selectRuntimeEngine(user, "OpenAI Engine");
    expect(screen.getByText(/Fetching available models from provider/i)).toBeInTheDocument();
  });

  it("lists discovered provider models as clickable tags and fills the field on click", async () => {
    const user = userEvent.setup();
    setup({
      catalog: {
        open: true,
        discoveredModelsByEngine: { "engine-1": ["gpt-4o-mini", "gpt-4o"] },
      },
    });
    await selectRuntimeEngine(user, "OpenAI Engine");

    // "gpt-4o-mini" also appears in the table as the Fast Chat row's
    // provider-model subtext, so scope the lookup to the drawer.
    const tag = await drawer().findByText("gpt-4o-mini");
    await user.click(tag);

    expect(drawer().getByLabelText("Provider model name")).toHaveValue("gpt-4o-mini");
  });

  it("auto-fills embedding dimensions when a matching model tag is clicked (create mode)", async () => {
    mockMatchEmbeddingDimension.mockReturnValue(1536);
    const user = userEvent.setup();
    setup({
      catalog: {
        open: true,
        isEmbedding: true,
        editingModel: null,
        discoveredModelsByEngine: { "engine-1": ["text-embed-3-small"] },
      },
    });
    await selectRuntimeEngine(user, "OpenAI Engine");

    const tag = await screen.findByText(/text-embed-3-small \(1536d\)/);
    await user.click(tag);

    expect(screen.getByLabelText("Embedding dimensions")).toHaveValue("1536");
  });

  it("shows an engine probe error when provided", () => {
    setup({ catalog: { open: true, engineProbeError: "Engine did not respond." } });
    expect(screen.getByText("Engine did not respond.")).toBeInTheDocument();
  });

  it("shows loading state on the Save button while saving", () => {
    setup({ catalog: { open: true, saving: true } });
    const saveButton = screen.getByRole("button", { name: /save model/i });
    expect(saveButton.querySelector(".anticon-loading")).toBeTruthy();
  });

  it("disables Cancel while saving", () => {
    setup({ catalog: { open: true, saving: true } });
    expect(screen.getByRole("button", { name: /cancel/i })).toBeDisabled();
  });
});
