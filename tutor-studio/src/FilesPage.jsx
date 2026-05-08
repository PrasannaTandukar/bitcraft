import { useState, useEffect, useRef } from "react";
import { useSelector } from "react-redux";

const DOCUMENTS_URL = "http://localhost:8000/api/documents";

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

function FileRow({ doc, index, onDelete }) {
  const [hovered, setHovered] = useState(false);
  const [deleteBtnHovered, setDeleteBtnHovered] = useState(false);

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
        gridTemplateColumns: "1fr 2fr 1fr auto",
        gap: "12px",
        alignItems: "center",
        padding: "14px 28px",
        background: hovered ? "#f7f8fc" : "transparent",
        transition: "background 0.15s ease",
        animation: "fadeSlideIn 0.3s ease both",
        animationDelay: `${index * 0.04}s`,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: "10px", gridColumn: "1 / 3" }}>
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
        color: "#999", textAlign: "right",
      }}>
        {formattedDate}
      </span>

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
  const [deleting, setDeleting] = useState(false);
  const [addHovered, setAddHovered] = useState(false);
  const [refreshHovered, setRefreshHovered] = useState(false);
  const fileInputRef = useRef(null);
  const token = useSelector((state) => state.auth.token);

  const fetchDocs = (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    fetch(DOCUMENTS_URL, {
      headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
    })
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        return res.json();
      })
      .then((data) => {
        setDocs(data);
        setError(null);
      })
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
      const res = await fetch(DOCUMENTS_URL, {
        method: "POST",
        headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ name: file.name, mimeType: file.type || null, sourcePath: null, rawText: null }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      const newDoc = await res.json();
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
      const res = await fetch(`${DOCUMENTS_URL}/${confirmDoc.id}`, {
        method: "DELETE",
        headers: { "Authorization": `Bearer ${token}` },
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
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

      <div style={{ height: "100%", background: "#ffffff", padding: "24px", fontFamily: "'DM Mono', monospace" }}>
        <div style={{ maxWidth: "860px", margin: "0 auto" }}>

          {/* Toolbar */}
          <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: "16px", gap: "8px", alignItems: "center" }}>
            {uploadError && (
              <span style={{ fontSize: "11px", color: "#e94560", marginRight: "4px" }}>{uploadError}</span>
            )}
            <input ref={fileInputRef} type="file" style={{ display: "none" }} onChange={handleFileChange} />

            {/* Refresh */}
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

            {/* Add File */}
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

          <div style={{ background: "#ffffff", borderRadius: "6px", overflow: "hidden" }}>
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
              <FileRow key={doc.id} doc={doc} index={i} onDelete={setConfirmDoc} />
            ))}
          </div>
        </div>
      </div>
    </>
  );
}