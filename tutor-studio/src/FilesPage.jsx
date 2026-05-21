import { useState, useEffect, useRef } from "react";
import { apiFetch } from "./api/client.js";

const DOCUMENTS_PATH = "/api/documents";

const mimeIcon = (mimeType) => {
  if (!mimeType) return "📄";
  if (mimeType.startsWith("image/")) return "🖼️";
  if (mimeType.startsWith("video/")) return "🎬";
  if (mimeType.startsWith("audio/")) return "🎵";
  if (mimeType === "application/pdf") return "📕";
  if (mimeType.includes("spreadsheet") || mimeType.includes("excel")) return "📊";
  if (mimeType.includes("presentation") || mimeType.includes("powerpoint")) return "📊";
  if (mimeType.includes("word") || mimeType.includes("document")) return "📝";
  if (mimeType.startsWith("text/")) return "📄";
  return "📦";
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

function ConfirmDialog({ fileName, onConfirm, onCancel, loading }) {
  return (
    <div style={{
      position: "fixed", inset: 0, background: "rgba(0,0,0,0.3)",
      display: "flex", alignItems: "center", justifyContent: "center",
      zIndex: 1000,
    }}>
      <div style={{
        background: "#fff", borderRadius: "8px", padding: "28px 32px",
        border: "1px solid #e8eaf0", maxWidth: "380px", width: "90%",
        boxShadow: "0 8px 32px rgba(0,0,0,0.12)",
        fontFamily: "'DM Mono', monospace",
      }}>
        <p style={{ fontSize: "13px", color: "#1a1a2e", marginBottom: "6px", fontWeight: "500" }}>
          Delete file?
        </p>
        <p style={{ fontSize: "12px", color: "#999", marginBottom: "24px", wordBreak: "break-all" }}>
          "{fileName}" will be permanently deleted.
        </p>
        <div style={{ display: "flex", gap: "8px", justifyContent: "flex-end" }}>
          <button
            onClick={onCancel}
            disabled={loading}
            style={{
              fontFamily: "'DM Mono', monospace", fontSize: "11px",
              padding: "6px 16px", border: "1px solid #e8eaf0",
              borderRadius: "4px", background: "#fff", color: "#555",
              cursor: "pointer",
            }}
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={loading}
            style={{
              fontFamily: "'DM Mono', monospace", fontSize: "11px",
              padding: "6px 16px", border: "1px solid #e94560",
              borderRadius: "4px", background: "#e94560", color: "#fff",
              cursor: loading ? "not-allowed" : "pointer",
              opacity: loading ? 0.6 : 1,
              display: "flex", alignItems: "center", gap: "6px",
            }}
          >
            {loading ? "Deleting…" : "Delete"}
          </button>
        </div>
      </div>
    </div>
  );
}

function VectorizeModal({ doc, onClose }) {
  const [form, setForm] = useState({
    embeddingProvider: "",
    embeddingModel: "",
    maxChunkLength: 512,
    embeddingModelId: "",
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(false);

  const set = (field, value) => setForm((f) => ({ ...f, [field]: value }));

  const handleSubmit = async () => {
    if (!form.embeddingModelId.trim()) { setError("Embedding Model ID is required."); return; }
    setSaving(true);
    setError(null);
    try {
      await apiFetch(`${DOCUMENTS_PATH}/${doc.id}/vectorize`, {
        method: "POST",
        json: {
          embeddingProvider: form.embeddingProvider || null,
          embeddingModel: form.embeddingModel || null,
          maxChunkLength: Number(form.maxChunkLength) || 512,
          embeddingModelId: form.embeddingModelId,
        },
      });
      setSuccess(true);
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
        border: "1px solid #e8eaf0", width: "100%", maxWidth: "480px",
        boxShadow: "0 8px 32px rgba(0,0,0,0.12)",
        fontFamily: "'DM Mono', monospace",
        maxHeight: "90vh", overflowY: "auto",
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
          <span style={{ fontSize: "13px", fontWeight: "600", color: "#1a1a2e" }}>Vectorize File</span>
          <button onClick={onClose} style={{ background: "none", border: "none", fontSize: "18px", cursor: "pointer", color: "#999", lineHeight: 1 }}>×</button>
        </div>
        <p style={{ fontSize: "11px", color: "#999", marginBottom: "24px", wordBreak: "break-all" }}>{doc.name}</p>

        {success ? (
          <div style={{ textAlign: "center", padding: "24px 0" }}>
            <div style={{ fontSize: "24px", marginBottom: "8px" }}>✓</div>
            <p style={{ fontSize: "13px", color: "#276749" }}>Vectorization started successfully.</p>
            <button
              onClick={onClose}
              style={{
                marginTop: "16px", fontFamily: "'DM Mono', monospace", fontSize: "11px",
                padding: "7px 16px", border: "1px solid #e8eaf0",
                borderRadius: "4px", background: "#fff", color: "#555", cursor: "pointer",
              }}
            >
              Close
            </button>
          </div>
        ) : (
          <>
            <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
              <div>
                <label style={labelStyle}>Embedding Model ID *</label>
                <input
                  style={inputStyle}
                  value={form.embeddingModelId}
                  onChange={(e) => set("embeddingModelId", e.target.value)}
                  placeholder="3fa85f64-5717-4562-b3fc-2c963f66afa6"
                />
              </div>

              <div>
                <label style={labelStyle}>Embedding Provider</label>
                <input
                  style={inputStyle}
                  value={form.embeddingProvider}
                  onChange={(e) => set("embeddingProvider", e.target.value)}
                  placeholder="ollama"
                />
              </div>

              <div>
                <label style={labelStyle}>Embedding Model</label>
                <input
                  style={inputStyle}
                  value={form.embeddingModel}
                  onChange={(e) => set("embeddingModel", e.target.value)}
                  placeholder="text-embedding-3-small"
                />
              </div>

              <div>
                <label style={labelStyle}>Max Chunk Length</label>
                <input
                  style={inputStyle}
                  type="number"
                  value={form.maxChunkLength}
                  onChange={(e) => set("maxChunkLength", e.target.value)}
                  placeholder="512"
                />
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
                  padding: "7px 16px", border: "1px solid #7c3aed",
                  borderRadius: "4px", background: "#7c3aed", color: "#fff",
                  cursor: saving ? "not-allowed" : "pointer",
                  opacity: saving ? 0.6 : 1,
                  display: "flex", alignItems: "center", gap: "6px",
                }}
              >
                {saving ? (
                  <>
                    <div style={{ width: "10px", height: "10px", border: "2px solid #fff", borderTop: "2px solid transparent", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                    Vectorizing…
                  </>
                ) : "Vectorize"}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function FileRow({ doc, index, onDelete, onVectorize }) {
  const [hovered, setHovered] = useState(false);
  const [deleteBtnHovered, setDeleteBtnHovered] = useState(false);
  const [vectorizeBtnHovered, setVectorizeBtnHovered] = useState(false);

  const date = doc.lastModifiedUtc
    ? new Date(doc.lastModifiedUtc)
    : new Date(doc.createdUtc);

  const formattedDate = date.toLocaleDateString(undefined, {
    year: "numeric", month: "short", day: "numeric",
  });

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: "grid",
        gridTemplateColumns: "1fr auto auto auto",
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
      <div style={{ display: "flex", alignItems: "center", gap: "10px", minWidth: 0 }}>
        <span style={{ fontSize: "18px", flexShrink: 0 }}>{mimeIcon(doc.mimeType)}</span>
        <span style={{
          fontFamily: "'DM Mono', monospace", fontSize: "13px", color: "#1a1a2e",
          overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
        }}>
          {doc.name}
        </span>
      </div>

      <span style={{
        fontFamily: "'DM Mono', monospace", fontSize: "11px",
        color: "#999", whiteSpace: "nowrap",
      }}>
        {formattedDate}
      </span>

      <button
        onClick={() => onVectorize(doc)}
        onMouseEnter={() => setVectorizeBtnHovered(true)}
        onMouseLeave={() => setVectorizeBtnHovered(false)}
        style={{
          fontFamily: "'DM Mono', monospace", fontSize: "11px",
          padding: "4px 10px", border: "1px solid #7c3aed",
          borderRadius: "4px",
          background: vectorizeBtnHovered ? "#7c3aed" : "#fff",
          color: vectorizeBtnHovered ? "#fff" : "#7c3aed",
          cursor: "pointer", transition: "background 0.15s, color 0.15s",
          whiteSpace: "nowrap",
        }}
      >
        Vectorize
      </button>

      <button
        onClick={() => onDelete(doc)}
        onMouseEnter={() => setDeleteBtnHovered(true)}
        onMouseLeave={() => setDeleteBtnHovered(false)}
        style={{
          fontFamily: "'DM Mono', monospace", fontSize: "11px",
          padding: "4px 10px", border: "1px solid #e94560",
          borderRadius: "4px",
          background: deleteBtnHovered ? "#e94560" : "#fff",
          color: deleteBtnHovered ? "#fff" : "#e94560",
          cursor: "pointer", transition: "background 0.15s, color 0.15s",
          whiteSpace: "nowrap",
        }}
      >
        Delete
      </button>
    </div>
  );
}

export default function FilesPage() {
  const [docs, setDocs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState(null);
  const [confirmDoc, setConfirmDoc] = useState(null);
  const [vectorizeDoc, setVectorizeDoc] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [addHovered, setAddHovered] = useState(false);
  const [refreshHovered, setRefreshHovered] = useState(false);
  const fileInputRef = useRef(null);

  const fetchDocs = (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    apiFetch(DOCUMENTS_PATH)
      .then((data) => { setDocs(data); setError(null); })
      .catch((err) => setError(err.message))
      .finally(() => { setLoading(false); setRefreshing(false); });
  };

  useEffect(() => { fetchDocs(); }, []);

  const handleFileChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    setUploadError(null);
    try {
      const newDoc = await apiFetch(DOCUMENTS_PATH, {
        method: "POST",
        json: { name: file.name, mimeType: file.type || null, sourcePath: null, rawText: null },
      });
      setDocs((prev) => [newDoc, ...prev]);
    } catch (err) {
      setUploadError(err.message);
    } finally {
      setUploading(false);
      e.target.value = "";
    }
  };

  const handleDeleteConfirm = async () => {
    if (!confirmDoc) return;
    setDeleting(true);
    try {
      await apiFetch(`${DOCUMENTS_PATH}/${confirmDoc.id}`, {
        method: "DELETE",
        parse: "none",
      });
      setDocs((prev) => prev.filter((d) => d.id !== confirmDoc.id));
      setConfirmDoc(null);
    } catch (err) {
      setUploadError(err.message);
      setConfirmDoc(null);
    } finally {
      setDeleting(false);
    }
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

      {confirmDoc && (
        <ConfirmDialog
          fileName={confirmDoc.name}
          onConfirm={handleDeleteConfirm}
          onCancel={() => setConfirmDoc(null)}
          loading={deleting}
        />
      )}

      {vectorizeDoc && (
        <VectorizeModal
          doc={vectorizeDoc}
          onClose={() => setVectorizeDoc(null)}
        />
      )}

      <div style={{ background: "#ffffff", padding: "24px", fontFamily: "'DM Mono', monospace" }}>
        <div style={{ maxWidth: "860px", margin: "0 auto" }}>

          <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: "16px", gap: "8px", alignItems: "center" }}>
            {uploadError && (
              <span style={{ fontSize: "11px", color: "#e94560", marginRight: "4px" }}>{uploadError}</span>
            )}
            <input ref={fileInputRef} type="file" style={{ display: "none" }} onChange={handleFileChange} />

            <button
              onClick={() => fetchDocs(true)}
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
              onClick={() => fileInputRef.current?.click()}
              disabled={uploading}
              onMouseEnter={() => setAddHovered(true)}
              onMouseLeave={() => setAddHovered(false)}
              style={{
                fontFamily: "'DM Mono', monospace", fontSize: "11px",
                letterSpacing: "0.08em", padding: "7px 16px",
                border: "1px solid #3b9ede", borderRadius: "4px",
                background: addHovered ? "#3b9ede" : "#fff",
                color: addHovered ? "#fff" : "#3b9ede",
                cursor: uploading ? "not-allowed" : "pointer",
                opacity: uploading ? 0.6 : 1,
                transition: "background 0.15s, color 0.15s",
                display: "flex", alignItems: "center", gap: "6px",
              }}
            >
              {uploading ? (
                <>
                  <div style={{ width: "10px", height: "10px", border: "2px solid #3b9ede", borderTop: "2px solid transparent", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                  Uploading…
                </>
              ) : <>+ Add File</>}
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
                <span style={{ fontSize: "13px", color: "#e94560" }}>Failed to load files</span>
                <span style={{ fontSize: "11px", color: "#999" }}>{error}</span>
              </div>
            )}

            {!loading && !error && docs.length === 0 && (
              <div style={{ padding: "64px 24px", textAlign: "center", fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>
                NO FILES FOUND
              </div>
            )}

            {!loading && !error && docs.map((doc, i) => (
              <FileRow
                key={doc.id}
                doc={doc}
                index={i}
                onDelete={setConfirmDoc}
                onVectorize={setVectorizeDoc}
              />
            ))}
          </div>
        </div>
      </div>
    </>
  );
}
