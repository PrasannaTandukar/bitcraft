import { useState, useEffect } from "react";
import { apiFetch } from "./api/client.js";

const API_PATH = "/api/admin/interaction-models";
const ENGINES_PATH = "/api/admin/runtime-engines";

const EMPTY_FORM = {
  name: "",
  runtimeEngineId: "",
  providerModelName: "",
  settingsJson: "",
  isEnabled: true,
};

function AddModelModal({ onClose, onSaved }) {
  const [form, setForm] = useState(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [engines, setEngines] = useState([]);
  const [enginesLoading, setEnginesLoading] = useState(true);

  const set = (field, value) => setForm((f) => ({ ...f, [field]: value }));

  useEffect(() => {
    apiFetch(ENGINES_PATH)
      .then((data) => {
        const enabled = data.filter((e) => e.isEnabled);
        setEngines(enabled);
        if (enabled.length > 0) set("runtimeEngineId", enabled[0].id);
      })
      .catch(() => setEngines([]))
      .finally(() => setEnginesLoading(false));
  }, []);

  const handleSubmit = async () => {
    if (!form.name.trim()) { setError("Name is required."); return; }
    if (!form.runtimeEngineId) { setError("Runtime Engine is required."); return; }
    setSaving(true);
    setError(null);
    try {
      const newModel = await apiFetch(API_PATH, {
        method: "POST",
        json: {
          name: form.name,
          runtimeEngineId: form.runtimeEngineId,
          providerModelName: form.providerModelName || null,
          settingsJson: form.settingsJson || null,
          isEnabled: form.isEnabled,
        },
      });
      onSaved(newModel);
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const inputStyle = {
    fontFamily: "'DM Mono', monospace",
    fontSize: "12px",
    padding: "7px 10px",
    border: "1px solid #e8eaf0",
    borderRadius: "4px",
    background: "#fff",
    color: "#1a1a2e",
    outline: "none",
    width: "100%",
    boxSizing: "border-box",
  };

  const labelStyle = {
    fontFamily: "'DM Mono', monospace",
    fontSize: "10px",
    letterSpacing: "0.12em",
    textTransform: "uppercase",
    color: "#99a",
    marginBottom: "5px",
    display: "block",
  };

  return (
    <div style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,0.3)",
      display: "flex", alignItems: "center", justifyContent: "center",
      zIndex: 1000,
    }}>
      <div style={{
        background: "#fff", borderRadius: "8px", padding: "32px",
        border: "1px solid #e8eaf0", width: "100%", maxWidth: "480px",
        boxShadow: "0 8px 32px rgba(0,0,0,0.12)",
        fontFamily: "'DM Mono', monospace",
        maxHeight: "90vh", overflowY: "auto",
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "24px" }}>
          <span style={{ fontSize: "13px", fontWeight: "600", color: "#1a1a2e" }}>Add Interaction Model</span>
          <button onClick={onClose} style={{ background: "none", border: "none", fontSize: "18px", cursor: "pointer", color: "#999", lineHeight: 1 }}>×</button>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
          <div>
            <label style={labelStyle}>Name *</label>
            <input style={inputStyle} value={form.name} onChange={(e) => set("name", e.target.value)} placeholder="My Interaction Model" />
          </div>

          <div>
            <label style={labelStyle}>Runtime Engine *</label>
            {enginesLoading ? (
              <div style={{ fontSize: "11px", color: "#999", padding: "7px 0" }}>Loading engines…</div>
            ) : engines.length === 0 ? (
              <div style={{ fontSize: "11px", color: "#e94560", padding: "7px 0" }}>No enabled runtime engines found. Add one first.</div>
            ) : (
              <select
                style={{ ...inputStyle, cursor: "pointer" }}
                value={form.runtimeEngineId}
                onChange={(e) => set("runtimeEngineId", e.target.value)}
              >
                {engines.map((engine) => (
                  <option key={engine.id} value={engine.id}>{engine.name}</option>
                ))}
              </select>
            )}
          </div>

          <div>
            <label style={labelStyle}>Provider Model Name</label>
            <input style={inputStyle} value={form.providerModelName} onChange={(e) => set("providerModelName", e.target.value)} placeholder="gpt-4o" />
          </div>

          <div>
            <label style={labelStyle}>Settings JSON</label>
            <input style={inputStyle} value={form.settingsJson} onChange={(e) => set("settingsJson", e.target.value)} placeholder='{"temperature": 0.7}' />
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
            <input
              id="isEnabled"
              type="checkbox"
              checked={form.isEnabled}
              onChange={(e) => set("isEnabled", e.target.checked)}
              style={{ width: "14px", height: "14px", cursor: "pointer" }}
            />
            <label htmlFor="isEnabled" style={{ ...labelStyle, marginBottom: 0, cursor: "pointer" }}>Enabled</label>
          </div>
        </div>

        {error && (
          <p style={{ fontSize: "11px", color: "#e94560", marginTop: "16px" }}>{error}</p>
        )}

        <div style={{ display: "flex", gap: "8px", justifyContent: "flex-end", marginTop: "24px" }}>
          <button
            onClick={onClose}
            disabled={saving}
            style={{
              fontFamily: "'DM Mono', monospace", fontSize: "11px",
              padding: "7px 16px", border: "1px solid #e8eaf0",
              borderRadius: "4px", background: "#fff", color: "#555",
              cursor: "pointer",
            }}
          >
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={saving || engines.length === 0}
            style={{
              fontFamily: "'DM Mono', monospace", fontSize: "11px",
              padding: "7px 16px", border: "1px solid #3b9ede",
              borderRadius: "4px", background: "#3b9ede", color: "#fff",
              cursor: (saving || engines.length === 0) ? "not-allowed" : "pointer",
              opacity: (saving || engines.length === 0) ? 0.6 : 1,
              display: "flex", alignItems: "center", gap: "6px",
            }}
          >
            {saving ? (
              <>
                <div style={{ width: "10px", height: "10px", border: "2px solid #fff", borderTop: "2px solid transparent", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                Saving…
              </>
            ) : "Save Model"}
          </button>
        </div>
      </div>
    </div>
  );
}

function ModelRow({ model, index }) {
  const [hovered, setHovered] = useState(false);

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: "grid",
        gridTemplateColumns: "1fr auto",
        gap: "12px",
        alignItems: "center",
        padding: "14px 28px",
        background: hovered ? "#f7f8fc" : "transparent",
        transition: "background 0.15s ease",
        animation: "fadeSlideIn 0.3s ease both",
        animationDelay: `${index * 0.04}s`,
        borderBottom: "1px solid #f0f0f5",
      }}
    >
      <div>
        <span style={{
          fontFamily: "'DM Mono', monospace",
          fontSize: "13px",
          color: "#1a1a2e",
          display: "block",
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "nowrap",
        }}>
          {model.name}
        </span>
        {model.providerModelName && (
          <span style={{
            fontFamily: "'DM Mono', monospace",
            fontSize: "11px",
            color: "#999",
            display: "block",
            marginTop: "2px",
          }}>
            {model.providerModelName}
          </span>
        )}
      </div>

      <span style={{
        fontFamily: "'DM Mono', monospace",
        fontSize: "11px",
        letterSpacing: "0.08em",
        textTransform: "uppercase",
        padding: "3px 10px",
        borderRadius: "2px",
        border: `1px solid ${model.isEnabled ? "#38a169" : "#ccc"}`,
        background: model.isEnabled ? "#f0fff4" : "#fafafa",
        color: model.isEnabled ? "#276749" : "#999",
        whiteSpace: "nowrap",
      }}>
        {model.isEnabled ? "Enabled" : "Disabled"}
      </span>
    </div>
  );
}

export default function InteractionModelsPage() {
  const [models, setModels] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [refreshHovered, setRefreshHovered] = useState(false);
  const [addHovered, setAddHovered] = useState(false);

  const fetchModels = (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    apiFetch(API_PATH)
      .then((data) => { setModels(data); setError(null); })
      .catch((err) => setError(err.message))
      .finally(() => { setLoading(false); setRefreshing(false); });
  };

  useEffect(() => { fetchModels(); }, []);

  const handleSaved = (newModel) => {
    setModels((prev) => [newModel, ...prev]);
    setShowModal(false);
  };

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=DM+Mono:wght@300;400;500&display=swap');
        @keyframes fadeSlideIn {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes spin { to { transform: rotate(360deg); } }
      `}</style>

      {showModal && (
        <AddModelModal
          onClose={() => setShowModal(false)}
          onSaved={handleSaved}
        />
      )}

      <div style={{ background: "#ffffff", padding: "24px", fontFamily: "'DM Mono', monospace" }}>
        <div style={{ maxWidth: "860px", margin: "0 auto" }}>

          <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: "16px", gap: "8px", alignItems: "center" }}>
            <button
              onClick={() => fetchModels(true)}
              disabled={refreshing}
              onMouseEnter={() => setRefreshHovered(true)}
              onMouseLeave={() => setRefreshHovered(false)}
              style={{
                fontFamily: "'DM Mono', monospace", fontSize: "11px",
                letterSpacing: "0.08em", padding: "7px 14px",
                border: "1px solid #e8eaf0", borderRadius: "4px",
                background: refreshHovered ? "#f7f8fc" : "#fff",
                color: "#555", cursor: refreshing ? "not-allowed" : "pointer",
                opacity: refreshing ? 0.6 : 1,
                transition: "background 0.15s",
                display: "flex", alignItems: "center", gap: "6px",
              }}
            >
              {refreshing ? (
                <div style={{ width: "10px", height: "10px", border: "2px solid #ccc", borderTop: "2px solid #555", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
              ) : "↻"} Refresh
            </button>

            <button
              onClick={() => setShowModal(true)}
              onMouseEnter={() => setAddHovered(true)}
              onMouseLeave={() => setAddHovered(false)}
              style={{
                fontFamily: "'DM Mono', monospace", fontSize: "11px",
                letterSpacing: "0.08em", padding: "7px 16px",
                border: "1px solid #3b9ede", borderRadius: "4px",
                background: addHovered ? "#3b9ede" : "#fff",
                color: addHovered ? "#fff" : "#3b9ede",
                cursor: "pointer",
                transition: "background 0.15s, color 0.15s",
              }}
            >
              + Add Model
            </button>
          </div>

          <div style={{ background: "#ffffff", borderRadius: "6px", overflow: "hidden", border: "1px solid #e8eaf0" }}>
            {loading && (
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", padding: "64px 24px", gap: "16px" }}>
                <div style={{ width: "28px", height: "28px", border: "2px solid #e8eaf0", borderTop: "2px solid #e94560", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                <span style={{ fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>LOADING…</span>
              </div>
            )}

            {error && (
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", padding: "64px 24px", gap: "12px" }}>
                <span style={{ fontSize: "24px" }}>⚠</span>
                <span style={{ fontSize: "13px", color: "#e94560" }}>Failed to load models</span>
                <span style={{ fontSize: "11px", color: "#999" }}>{error}</span>
              </div>
            )}

            {!loading && !error && models.length === 0 && (
              <div style={{ padding: "64px 24px", textAlign: "center", fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>
                NO INTERACTION MODELS FOUND
              </div>
            )}

            {!loading && !error && models.map((model, i) => (
              <ModelRow key={model.id} model={model} index={i} />
            ))}
          </div>
        </div>
      </div>
    </>
  );
}
