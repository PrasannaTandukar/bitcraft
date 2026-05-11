import { useState, useEffect } from "react";
import { useSelector } from "react-redux";

const TUTORS_URL = "http://localhost:8000/api/tutors";

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

function AddTutorModal({ onClose, onSaved, token }) {
  const [form, setForm] = useState({ name: "", description: "" });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const set = (field, value) => setForm((f) => ({ ...f, [field]: value }));

  const handleSubmit = async () => {
    if (!form.name.trim()) { setError("Name is required."); return; }
    setSaving(true);
    setError(null);
    try {
      const res = await fetch(TUTORS_URL, {
        method: "POST",
        headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ name: form.name.trim(), description: form.description || null }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      const newTutor = await res.json();
      onSaved(newTutor);
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
        border: "1px solid #e8eaf0", width: "100%", maxWidth: "440px",
        boxShadow: "0 8px 32px rgba(0,0,0,0.12)",
        fontFamily: "'DM Mono', monospace",
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "24px" }}>
          <span style={{ fontSize: "13px", fontWeight: "600", color: "#1a1a2e" }}>Add Tutor</span>
          <button onClick={onClose} style={{ background: "none", border: "none", fontSize: "18px", cursor: "pointer", color: "#999", lineHeight: 1 }}>×</button>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
          <div>
            <label style={labelStyle}>Name *</label>
            <input style={inputStyle} value={form.name} onChange={(e) => set("name", e.target.value)} placeholder="My Tutor" />
          </div>
          <div>
            <label style={labelStyle}>Description</label>
            <input style={inputStyle} value={form.description} onChange={(e) => set("description", e.target.value)} placeholder="A helpful tutor for…" />
          </div>
        </div>

        {error && <p style={{ fontSize: "11px", color: "#e94560", marginTop: "16px" }}>{error}</p>}

        <div style={{ display: "flex", gap: "8px", justifyContent: "flex-end", marginTop: "24px" }}>
          <button
            onClick={onClose}
            disabled={saving}
            style={{
              fontFamily: "'DM Mono', monospace", fontSize: "11px",
              padding: "7px 16px", border: "1px solid #e8eaf0",
              borderRadius: "4px", background: "#fff", color: "#555", cursor: "pointer",
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
            ) : "Save Tutor"}
          </button>
        </div>
      </div>
    </div>
  );
}

function TutorRow({ tutor, index }) {
  const [hovered, setHovered] = useState(false);

  const formattedDate = new Date(tutor.createdUtc).toLocaleDateString(undefined, {
    year: "numeric", month: "short", day: "numeric",
  });

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: "grid",
        gridTemplateColumns: "1fr 2fr auto",
        gap: "16px",
        alignItems: "center",
        padding: "14px 28px",
        background: hovered ? "#f7f8fc" : "transparent",
        transition: "background 0.15s ease",
        animation: "fadeSlideIn 0.3s ease both",
        animationDelay: `${index * 0.04}s`,
      }}
    >
      <span style={{
        fontFamily: "'DM Mono', monospace", fontSize: "13px", color: "#1a1a2e",
        overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
      }}>
        {tutor.name}
      </span>

      <span style={{
        fontFamily: "'DM Mono', monospace", fontSize: "12px", color: "#666",
        overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
      }}>
        {tutor.description || <span style={{ color: "#ccc" }}>—</span>}
      </span>

      <span style={{
        fontFamily: "'DM Mono', monospace", fontSize: "11px",
        color: "#999", whiteSpace: "nowrap",
      }}>
        {formattedDate}
      </span>
    </div>
  );
}

export default function TutorsPage() {
  const [tutors, setTutors] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [refreshHovered, setRefreshHovered] = useState(false);
  const [addHovered, setAddHovered] = useState(false);
  const token = useSelector((state) => state.auth.token);

  const fetchTutors = (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    fetch(TUTORS_URL, {
      headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
    })
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        return res.json();
      })
      .then((data) => { setTutors(data); setError(null); })
      .catch((err) => setError(err.message))
      .finally(() => { setLoading(false); setRefreshing(false); });
  };

  useEffect(() => { fetchTutors(); }, []);

  const handleSaved = (newTutor) => {
    setTutors((prev) => [newTutor, ...prev]);
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
        <AddTutorModal
          onClose={() => setShowModal(false)}
          onSaved={handleSaved}
          token={token}
        />
      )}

      <div style={{ height: "100%", background: "#ffffff", padding: "24px", fontFamily: "'DM Mono', monospace" }}>
        <div style={{ maxWidth: "860px", margin: "0 auto" }}>

          {/* Toolbar */}
          <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: "16px", gap: "8px", alignItems: "center" }}>
            <button
              onClick={() => fetchTutors(true)}
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
              + Add Tutor
            </button>
          </div>

          {/* Table Header */}
          <div style={{
            display: "grid", gridTemplateColumns: "1fr 2fr auto",
            gap: "16px", padding: "10px 28px",
            background: "#f7f8fc", borderRadius: "6px 6px 0 0",
            borderBottom: "1px solid #e8eaf0",
          }}>
            {["Name", "Description", "Created"].map((h) => (
              <span key={h} style={{ fontSize: "10px", letterSpacing: "0.15em", textTransform: "uppercase", color: "#99a", fontFamily: "'DM Mono', monospace" }}>
                {h}
              </span>
            ))}
          </div>

          <div style={{ background: "#ffffff", borderRadius: "0 0 6px 6px", overflow: "hidden" }}>
            {loading && (
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", padding: "64px 24px", gap: "16px" }}>
                <div style={{ width: "28px", height: "28px", border: "2px solid #e8eaf0", borderTop: "2px solid #e94560", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                <span style={{ fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>LOADING…</span>
              </div>
            )}

            {error && (
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", padding: "64px 24px", gap: "12px" }}>
                <span style={{ fontSize: "24px" }}>⚠</span>
                <span style={{ fontSize: "13px", color: "#e94560" }}>Failed to load tutors</span>
                <span style={{ fontSize: "11px", color: "#999" }}>{error}</span>
              </div>
            )}

            {!loading && !error && tutors.length === 0 && (
              <div style={{ padding: "64px 24px", textAlign: "center", fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>
                NO TUTORS FOUND
              </div>
            )}

            {!loading && !error && tutors.map((tutor, i) => (
              <TutorRow key={tutor.id} tutor={tutor} index={i} />
            ))}
          </div>
        </div>
      </div>
    </>
  );
}