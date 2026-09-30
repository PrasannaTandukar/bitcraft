import { useCallback, useEffect, useMemo, useState } from "react";
import { App } from "antd";
import { apiFetch, getApiErrorDescription } from "../../../api/client.js";
import {
  API_PATH,
  filterEnginesBySearch,
  resolveEndpointForProviderChange,
} from "../utils/runtimeEngineUtils.js";

/**
 * Encapsulates all data-fetching, CRUD, and connection-test state for
 * RuntimeEnginesPage. Takes the Antd `form` instance so it can drive
 * field resets/values, since that's inherent to the create/edit flow.
 *
 * @param {import("antd").FormInstance} form
 */
export default function useRuntimeEngines(form) {
  const { message } = App.useApp();

  const [engines, setEngines] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const [open, setOpen] = useState(false);
  const [editingEngine, setEditingEngine] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");
  const [search, setSearch] = useState("");
  const [deletingId, setDeletingId] = useState("");
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState(null);

  const load = useCallback((refresh = false) => {
    if (refresh) setRefreshing(true);
    else setLoading(true);
    apiFetch(API_PATH)
      .then((data) => {
        setEngines(data ?? []);
        setError("");
      })
      .catch((requestError) =>
        setError(
          getApiErrorDescription(
            requestError,
            "Could not load runtime engines.",
          ),
        ),
      )
      .finally(() => {
        setLoading(false);
        setRefreshing(false);
      });
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const filteredEngines = useMemo(
    () => filterEnginesBySearch(engines, search),
    [engines, search],
  );

  const openCreate = useCallback(() => {
    setEditingEngine(null);
    setTestResult(null);
    form.resetFields();
    setOpen(true);
  }, [form]);

  const openEdit = useCallback(
    (engine) => {
      setEditingEngine(engine);
      setTestResult(null);
      form.setFieldsValue({
        name: engine.name,
        providerType: engine.providerType,
        endpoint: engine.endpoint,
        apiKey: undefined,
        isEnabled: engine.isEnabled,
      });
      setOpen(true);
    },
    [form],
  );

  const handleProviderTypeChange = useCallback(
    (value) => {
      const currentEndpoint = form.getFieldValue("endpoint");
      const nextEndpoint = resolveEndpointForProviderChange(
        value,
        currentEndpoint,
      );
      if (nextEndpoint !== currentEndpoint) {
        form.setFieldValue("endpoint", nextEndpoint);
      }
    },
    [form],
  );

  const testConnection = useCallback(async () => {
    const values = form.getFieldsValue();
    if (!values.endpoint?.trim()) {
      message.error("Enter an endpoint to test.");
      return;
    }
    setTesting(true);
    setTestResult(null);
    try {
      const result = await apiFetch(`${API_PATH}/test`, {
        method: "POST",
        json: {
          providerType: Number(values.providerType ?? 0),
          endpoint: values.endpoint.trim(),
          apiKey: values.apiKey?.trim() || null,
        },
      });
      setTestResult(result);
    } catch (requestError) {
      setTestResult({
        isSuccess: false,
        latencyMs: 0,
        message: getApiErrorDescription(
          requestError,
          "Test connection request failed.",
        ),
      });
    } finally {
      setTesting(false);
    }
  }, [form, message]);

  const close = useCallback(() => {
    if (saving || testing) return;
    setOpen(false);
    setEditingEngine(null);
    setFormError("");
    setTestResult(null);
    form.resetFields();
  }, [saving, testing, form]);

  const save = useCallback(
    async (values) => {
      setSaving(true);
      setFormError("");
      const payload = {
        name: values.name.trim(),
        providerType: Number(values.providerType),
        endpoint: values.endpoint.trim(),
        apiKey: values.apiKey?.trim() || null,
        clearApiKey: values.clearApiKey === true,
        isEnabled: values.isEnabled,
      };
      try {
        if (editingEngine) {
          const updated = await apiFetch(`${API_PATH}/${editingEngine.id}`, {
            method: "PUT",
            json: payload,
          });
          setEngines((current) =>
            current.map((engine) =>
              engine.id === editingEngine.id
                ? (updated ?? { ...engine, ...payload })
                : engine,
            ),
          );
          message.success("Runtime engine updated.");
        } else {
          const created = await apiFetch(API_PATH, {
            method: "POST",
            json: payload,
          });
          setEngines((current) => [created, ...current]);
          message.success("Runtime engine created.");
        }
        setOpen(false);
        setEditingEngine(null);
        form.resetFields();
      } catch (requestError) {
        setFormError(
          getApiErrorDescription(
            requestError,
            `Could not ${editingEngine ? "update" : "create"} the runtime engine.`,
          ),
        );
      } finally {
        setSaving(false);
      }
    },
    [editingEngine, form, message],
  );

  const remove = useCallback(
    async (engine) => {
      setDeletingId(engine.id);
      try {
        await apiFetch(`${API_PATH}/${engine.id}`, {
          method: "DELETE",
          parse: "none",
        });
        setEngines((current) =>
          current.filter((item) => item.id !== engine.id),
        );
        message.success("Runtime engine deleted.");
      } catch (requestError) {
        message.error(
          getApiErrorDescription(
            requestError,
            "Could not delete the runtime engine.",
          ),
        );
      } finally {
        setDeletingId("");
      }
    },
    [message],
  );

  return {
    engines,
    loading,
    refreshing,
    error,
    open,
    setOpen,
    editingEngine,
    saving,
    formError,
    search,
    setSearch,
    deletingId,
    testing,
    testResult,
    setTestResult,
    filteredEngines,
    load,
    openCreate,
    openEdit,
    handleProviderTypeChange,
    testConnection,
    close,
    save,
    remove,
  };
}
