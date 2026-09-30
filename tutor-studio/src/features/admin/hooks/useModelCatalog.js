import { useCallback, useEffect, useMemo, useState } from "react";
import { App } from "antd";
import { apiFetch, getApiErrorDescription } from "../../../api/client.js";
import {
  ENGINES_PATH,
  catalogConfig,
  filterAndSortAvailableModels,
  filterModelsBySearch,
} from "../utils/modelCatalogUtils.js";

export default function useModelCatalog(kind, form) {
  const config = catalogConfig[kind];
  const isEmbedding = kind === "embedding";
  const { message } = App.useApp();

  const [models, setModels] = useState([]);
  const [engines, setEngines] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [engineError, setEngineError] = useState("");
  const [open, setOpen] = useState(false);
  const [editingModel, setEditingModel] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [search, setSearch] = useState("");
  const [deletingId, setDeletingId] = useState("");

  // Engine model discovery state
  const [discoveredModelsByEngine, setDiscoveredModelsByEngine] = useState({});
  const [fetchingEngineModels, setFetchingEngineModels] = useState(false);
  const [engineProbeError, setEngineProbeError] = useState("");

  const load = useCallback(
    async (refresh = false) => {
      if (refresh) setRefreshing(true);
      else setLoading(true);
      const [modelsResult, enginesResult] = await Promise.allSettled([
        apiFetch(config.apiPath),
        apiFetch(ENGINES_PATH),
      ]);
      if (modelsResult.status === "fulfilled") {
        setModels(modelsResult.value ?? []);
        setError("");
      } else
        setError(
          getApiErrorDescription(
            modelsResult.reason,
            `Could not load ${config.title.toLowerCase()}.`,
          ),
        );
      if (enginesResult.status === "fulfilled") {
        setEngines(
          (enginesResult.value ?? []).filter((engine) => engine.isEnabled),
        );
        setEngineError("");
      } else
        setEngineError(
          getApiErrorDescription(
            enginesResult.reason,
            "Runtime engines could not be loaded.",
          ),
        );
      setLoading(false);
      setRefreshing(false);
    },
    [config],
  );

  useEffect(() => {
    const timer = window.setTimeout(() => load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const loadEngineModels = useCallback(
    async (engineId, force = false) => {
      if (!engineId) return;
      if (!force && discoveredModelsByEngine[engineId] !== undefined) {
        return;
      }

      const engine = engines.find((e) => e.id === engineId);
      if (!engine || !engine.endpoint) return;

      setFetchingEngineModels(true);
      setEngineProbeError("");
      try {
        const result = await apiFetch(`${ENGINES_PATH}/test`, {
          method: "POST",
          json: {
            providerType: Number(engine.providerType ?? 0),
            endpoint: engine.endpoint.trim(),
            apiKey: engine.apiKey?.trim() || null,
          },
        });

        const rawModels = result?.availableModels ?? [];
        const topModels = filterAndSortAvailableModels(
          rawModels,
          isEmbedding,
          10,
        );
        setDiscoveredModelsByEngine((prev) => ({
          ...prev,
          [engineId]: topModels,
        }));
      } catch (requestError) {
        setEngineProbeError(
          getApiErrorDescription(
            requestError,
            "Could not query available models from engine.",
          ),
        );
        setDiscoveredModelsByEngine((prev) => ({
          ...prev,
          [engineId]: [],
        }));
      } finally {
        setFetchingEngineModels(false);
      }
    },
    [discoveredModelsByEngine, engines, isEmbedding],
  );

  const filteredModels = useMemo(
    () => filterModelsBySearch(models, search),
    [models, search],
  );

  const openCreate = useCallback(() => {
    setEditingModel(null);
    setEngineProbeError("");
    form.resetFields();
    if (engines.length > 0) {
      form.setFieldValue("runtimeEngineId", engines[0].id);
    }
    setOpen(true);
  }, [engines, form]);

  const openEdit = useCallback(
    (model) => {
      setEditingModel(model);
      setEngineProbeError("");
      form.setFieldsValue({
        name: model.name,
        runtimeEngineId: model.runtimeEngineId,
        providerModelName: model.providerModelName ?? undefined,
        settingsJson: model.settingsJson ?? undefined,
        isEnabled: model.isEnabled,
        supportsImageInput: model.supportsImageInput ?? false,
        supportsFunctionCalling: model.supportsFunctionCalling ?? false,
        supportsStructuredOutput: model.supportsStructuredOutput ?? false,
        embeddingDimensions: model.embeddingDimensions,
      });
      setOpen(true);
    },
    [form],
  );

  const close = useCallback(() => {
    if (saving) return;
    setOpen(false);
    setEditingModel(null);
    setFormError("");
    setEngineProbeError("");
    form.resetFields();
  }, [saving, form]);

  const save = useCallback(
    async (values) => {
      setSaving(true);
      setFormError("");
      try {
        const payload = {
          name: values.name.trim(),
          runtimeEngineId: values.runtimeEngineId,
          providerModelName: values.providerModelName?.trim() || null,
          settingsJson: values.settingsJson?.trim() || null,
          isEnabled: values.isEnabled,
        };
        if (isEmbedding && !editingModel)
          payload.embeddingDimensions = Number(values.embeddingDimensions);
        if (!isEmbedding) {
          payload.supportsImageInput = Boolean(values.supportsImageInput);
          payload.supportsFunctionCalling = Boolean(values.supportsFunctionCalling);
          payload.supportsStructuredOutput = Boolean(values.supportsStructuredOutput);
        }
        if (editingModel) {
          const updated = await apiFetch(
            `${config.apiPath}/${editingModel.id}`,
            { method: "PUT", json: payload },
          );
          setModels((current) =>
            current.map((model) =>
              model.id === editingModel.id
                ? (updated ?? { ...model, ...payload })
                : model,
            ),
          );
          message.success(`${config.singular} updated.`);
        } else {
          const created = await apiFetch(config.apiPath, {
            method: "POST",
            json: payload,
          });
          setModels((current) => [created, ...current]);
          message.success(`${config.singular} created.`);
        }
        setOpen(false);
        setEditingModel(null);
        form.resetFields();
      } catch (requestError) {
        setFormError(
          getApiErrorDescription(
            requestError,
            `Could not ${editingModel ? "update" : "create"} the ${config.singular}.`,
          ),
        );
      } finally {
        setSaving(false);
      }
    },
    [config, editingModel, form, isEmbedding, message],
  );

  const remove = useCallback(
    async (model) => {
      setDeletingId(model.id);
      try {
        await apiFetch(`${config.apiPath}/${model.id}`, {
          method: "DELETE",
          parse: "none",
        });
        setModels((current) => current.filter((item) => item.id !== model.id));
        message.success(`${config.singular} deleted.`);
      } catch (requestError) {
        message.error(
          getApiErrorDescription(
            requestError,
            `Could not delete the ${config.singular}.`,
          ),
        );
      } finally {
        setDeletingId("");
      }
    },
    [config, message],
  );

  return {
    config,
    isEmbedding,
    models,
    engines,
    loading,
    refreshing,
    error,
    engineError,
    open,
    setOpen,
    editingModel,
    saving,
    formError,
    search,
    setSearch,
    deletingId,
    discoveredModelsByEngine,
    fetchingEngineModels,
    engineProbeError,
    filteredModels,
    load,
    loadEngineModels,
    openCreate,
    openEdit,
    close,
    save,
    remove,
  };
}
