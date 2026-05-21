import { useState, useEffect } from "react";
import { apiFetch } from "./api/client.js";

const API_PATH = "/api/benchmark/templates";

const EMPTY_FORM = {
  key: "",
  name: "",
  description: "",
  guidancePath: "",
  isEnabled: true,
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

const SUPPORTED_METRICS = ["faithfulness", "answer_relevancy", "hallucination", "correctness"];
const DEFAULT_METRICS = SUPPORTED_METRICS.map((key) => ({ key, threshold: 0.5, enabled: true }));

function AddTemplateModal({ onClose, onSaved }) {
  const [form, setForm] = useState(EMPTY_FORM);
  const [metrics, setMetrics] = useState(DEFAULT_METRICS);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const set = (field, value) => setForm((f) => ({ ...f, [field]: value }));
  const setMetric = (key, field, value) =>
    setMetrics((prev) => prev.map((m) => m.key === key ? { ...m, [field]: value } : m));

  const handleSubmit = async () => {
    if (!form.key.trim()) { setError("Key is required."); return; }
    if (!form.name.trim()) { setError("Name is required."); return; }
    const enabledMetrics = metrics.filter((m) => m.enabled);
    if (enabledMetrics.length === 0) { setError("At least one metric must be enabled."); return; }
    const metricsJson = JSON.stringify(
      enabledMetrics.map((m) => ({ key: m.key, threshold: m.threshold, enabled: true }))
    );
    setSaving(true);
    setError(null);
    try {
      const newTemplate = await apiFetch(API_PATH, {
        method: "POST",
        json: {
          key: form.key,
          name: form.name,
          description: form.description || null,
          guidancePath: form.guidancePath || null,
          metricsJson,
          isEnabled: form.isEnabled,
        },
      });
      onSaved(newTemplate);
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,0.3)",
      display: "flex", alignItems: "center", justifyContent: "center",
      zIndex: 1000,
    }}>
      <div style={{
        background: "#fff", borderRadius: "8px", padding: "32px",
        border: "1px solid #e8eaf0", width: "100%", maxWidth: "520px",
        boxShadow: "0 8px 32px rgba(0,0,0,0.12)",
        fontFamily: "'DM Mono', monospace",
        maxHeight: "90vh", overflowY: "auto",
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "24px" }}>
          <span style={{ fontSize: "13px", fontWeight: "600", color: "#1a1a2e" }}>Add Benchmark Template</span>
          <button onClick={onClose} style={{ background: "none", border: "none", fontSize: "18px", cursor: "pointer", color: "#999", lineHeight: 1 }}>×</button>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
          <div>
            <label style={labelStyle}>Key *</label>
            <input style={inputStyle} value={form.key} onChange={(e) => set("key", e.target.value)} placeholder="my-benchmark-key" />
          </div>

          <div>
            <label style={labelStyle}>Name *</label>
            <input style={inputStyle} value={form.name} onChange={(e) => set("name", e.target.value)} placeholder="My Benchmark Template" />
          </div>

          <div>
            <label style={labelStyle}>Description</label>
            <textarea
              style={{ ...inputStyle, resize: "vertical", minHeight: "72px" }}
              value={form.description}
              onChange={(e) => set("description", e.target.value)}
              placeholder="Describe what this benchmark measures…"
            />
          </div>

          <div>
            <label style={labelStyle}>Guidance Path</label>
            <input style={inputStyle} value={form.guidancePath} onChange={(e) => set("guidancePath", e.target.value)} placeholder="/guidance/my-template.md" />
          </div>

          <div>
            <label style={labelStyle}>Metrics * <span style={{ color: "#bbb", textTransform: "none", letterSpacing: 0 }}>(enable &amp; set threshold 0–1)</span></label>
            <div style={{ display: "flex", flexDirection: "column", gap: "10px", padding: "12px", border: "1px solid #e8eaf0", borderRadius: "4px" }}>
              {metrics.map((m) => (
                <div key={m.key} style={{ display: "grid", gridTemplateColumns: "auto 1fr auto", gap: "10px", alignItems: "center" }}>
                  <input
                    type="checkbox"
                    checked={m.enabled}
                    onChange={(e) => setMetric(m.key, "enabled", e.target.checked)}
                    style={{ width: "14px", height: "14px", cursor: "pointer" }}
                  />
                  <span style={{ fontFamily: "'DM Mono', monospace", fontSize: "12px", color: m.enabled ? "#1a1a2e" : "#bbb" }}>
                    {m.key}
                  </span>
                  <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                    <input
                      type="range"
                      min="0" max="1" step="0.05"
                      value={m.threshold}
                      disabled={!m.enabled}
                      onChange={(e) => setMetric(m.key, "threshold", parseFloat(e.target.value))}
                      style={{ width: "80px", cursor: m.enabled ? "pointer" : "not-allowed", opacity: m.enabled ? 1 : 0.4 }}
                    />
                    <span style={{ fontFamily: "'DM Mono', monospace", fontSize: "11px", color: "#666", minWidth: "30px", textAlign: "right" }}>
                      {m.threshold.toFixed(2)}
                    </span>
                  </div>
                </div>
              ))}
            </div>
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
            disabled={saving}
            style={{
              fontFamily: "'DM Mono', monospace", fontSize: "11px",
              padding: "7px 16px", border: "1px solid #3b9ede",
              borderRadius: "4px", background: "#3b9ede", color: "#fff",
              cursor: saving ? "not-allowed" : "pointer",
              opacity: saving ? 0.6 : 1,
              display: "flex", alignItems: "center", gap: "6px",
            }}
          >
            {saving ? (
              <>
                <div style={{ width: "10px", height: "10px", border: "2px solid #fff", borderTop: "2px solid transparent", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                Saving…
              </>
            ) : "Save Template"}
          </button>
        </div>
      </div>
    </div>
  );
}

function TemplateRow({ template, index }) {
  const [hovered, setHovered] = useState(false);

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: "grid",
        gridTemplateColumns: "1fr auto auto",
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
          {template.name}
        </span>
        <span style={{
          fontFamily: "'DM Mono', monospace",
          fontSize: "11px",
          color: "#999",
          display: "block",
          marginTop: "2px",
        }}>
          {template.key}
          {template.description && ` · ${template.description}`}
        </span>
      </div>

      {template.guidancePath && (
        <span style={{
          fontFamily: "'DM Mono', monospace",
          fontSize: "11px",
          color: "#666",
          whiteSpace: "nowrap",
          overflow: "hidden",
          textOverflow: "ellipsis",
          maxWidth: "160px",
        }}>
          {template.guidancePath}
        </span>
      )}

      <span style={{
        fontFamily: "'DM Mono', monospace",
        fontSize: "11px",
        letterSpacing: "0.08em",
        textTransform: "uppercase",
        padding: "3px 10px",
        borderRadius: "2px",
        border: `1px solid ${template.isEnabled ? "#38a169" : "#ccc"}`,
        background: template.isEnabled ? "#f0fff4" : "#fafafa",
        color: template.isEnabled ? "#276749" : "#999",
        whiteSpace: "nowrap",
      }}>
        {template.isEnabled ? "Enabled" : "Disabled"}
      </span>
    </div>
  );
}

export default function BenchmarkTemplatesPage() {
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [refreshHovered, setRefreshHovered] = useState(false);
  const [addHovered, setAddHovered] = useState(false);

  const fetchTemplates = (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    apiFetch(API_PATH)
      .then((data) => { setTemplates(data); setError(null); })
      .catch((err) => setError(err.message))
      .finally(() => { setLoading(false); setRefreshing(false); });
  };

  useEffect(() => { fetchTemplates(); }, []);

  const handleSaved = (newTemplate) => {
    setTemplates((prev) => [newTemplate, ...prev]);
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
        <AddTemplateModal
          onClose={() => setShowModal(false)}
          onSaved={handleSaved}
        />
      )}

      <div style={{ background: "#ffffff", padding: "24px", fontFamily: "'DM Mono', monospace" }}>
        <div style={{ maxWidth: "860px", margin: "0 auto" }}>

          <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: "16px", gap: "8px", alignItems: "center" }}>
            <button
              onClick={() => fetchTemplates(true)}
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
              + Add Template
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
                <span style={{ fontSize: "13px", color: "#e94560" }}>Failed to load templates</span>
                <span style={{ fontSize: "11px", color: "#999" }}>{error}</span>
              </div>
            )}

            {!loading && !error && templates.length === 0 && (
              <div style={{ padding: "64px 24px", textAlign: "center", fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>
                NO BENCHMARK TEMPLATES FOUND
              </div>
            )}

            {!loading && !error && templates.map((template, i) => (
              <TemplateRow key={template.id} template={template} index={i} />
            ))}
          </div>
        </div>
      </div>
    </>
  );
}
