import { useState, useEffect } from "react";
import { useSelector } from "react-redux";

const BASE = "http://localhost:8000/api/admin";
const USERS_URL = `${BASE}/users`;
const ROLES_URL = `${BASE}/roles`;

const roleColors = [
  { bg: "#fff0f2", border: "#e94560", text: "#c0293f" },
  { bg: "#eff7ff", border: "#3b9ede", text: "#1a6fa8" },
  { bg: "#f0fff4", border: "#38a169", text: "#276749" },
  { bg: "#fffbeb", border: "#d97706", text: "#92520a" },
];

function RoleBadge({ role, index }) {
  const color = roleColors[index % roleColors.length];
  return (
    <span style={{
      display: "inline-block",
      padding: "2px 10px",
      fontSize: "11px",
      fontFamily: "'DM Mono', monospace",
      letterSpacing: "0.08em",
      textTransform: "uppercase",
      background: color.bg,
      border: `1px solid ${color.border}`,
      color: color.text,
      borderRadius: "2px",
      marginRight: "6px",
      marginBottom: "4px",
    }}>
      {role}
    </span>
  );
}

const selectStyle = {
  fontFamily: "'DM Mono', monospace",
  fontSize: "11px",
  padding: "5px 8px",
  border: "1px solid #e8eaf0",
  borderRadius: "4px",
  background: "#fff",
  color: "#1a1a2e",
  cursor: "pointer",
  outline: "none",
  minWidth: "120px",
};

const btnBase = (color, hovered) => ({
  fontFamily: "'DM Mono', monospace",
  fontSize: "11px",
  letterSpacing: "0.06em",
  padding: "5px 12px",
  border: `1px solid ${color}`,
  borderRadius: "4px",
  background: hovered ? color : "#fff",
  color: hovered ? "#fff" : color,
  cursor: "pointer",
  whiteSpace: "nowrap",
  transition: "background 0.15s, color 0.15s",
});

function UserRow({ user: initialUser, index, allRoles, token, onUpdate }) {
  const [user, setUser] = useState(initialUser);
  const [addRole, setAddRole] = useState("");
  const [revokeRole, setRevokeRole] = useState("");
  const [addLoading, setAddLoading] = useState(false);
  const [revokeLoading, setRevokeLoading] = useState(false);
  const [addHover, setAddHover] = useState(false);
  const [revokeHover, setRevokeHover] = useState(false);

  const availableToAdd = allRoles.filter((r) => !user.roles.includes(r));
  const availableToRevoke = user.roles;

  useEffect(() => {
    setAddRole(availableToAdd.length > 0 ? availableToAdd[0] : "");
  }, [user.roles, allRoles]);

  useEffect(() => {
    setRevokeRole(availableToRevoke.length > 0 ? availableToRevoke[0] : "");
  }, [user.roles]);

  const handleAdd = async () => {
    if (!addRole) return;
    setAddLoading(true);
    try {
      const res = await fetch(`${USERS_URL}/${user.userId}/roles`, {
        method: "POST",
        headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
        body: JSON.stringify({ role: addRole }),
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const updated = { ...user, roles: [...user.roles, addRole] };
      setUser(updated);
      onUpdate(updated);
    } catch (e) {
      alert(`Failed to add role: ${e.message}`);
    } finally {
      setAddLoading(false);
    }
  };

  const handleRevoke = async () => {
    if (!revokeRole) return;
    setRevokeLoading(true);
    try {
      const res = await fetch(`${USERS_URL}/${user.userId}/roles/${revokeRole}`, {
        method: "DELETE",
        headers: { "Authorization": `Bearer ${token}` },
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const updated = { ...user, roles: user.roles.filter((r) => r !== revokeRole) };
      setUser(updated);
      onUpdate(updated);
    } catch (e) {
      alert(`Failed to revoke role: ${e.message}`);
    } finally {
      setRevokeLoading(false);
    }
  };

  return (
    <div style={{
      display: "grid",
      gridTemplateColumns: "2fr 2fr 2fr 2fr",
      gap: "12px",
      alignItems: "center",
      padding: "16px 28px",
      borderBottom: "1px solid #e8eaf0",
      animation: "fadeSlideIn 0.3s ease both",
      animationDelay: `${index * 0.05}s`,
    }}>
      {/* Email */}
      <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
        <div style={{
          width: "30px", height: "30px", borderRadius: "50%",
          background: `hsl(${(index * 47) % 360}, 55%, 35%)`,
          display: "flex", alignItems: "center", justifyContent: "center",
          fontSize: "12px", fontWeight: "700", color: "#fff", flexShrink: 0,
          fontFamily: "'DM Mono', monospace",
        }}>
          {user.email[0].toUpperCase()}
        </div>
        <span style={{
          fontFamily: "'DM Mono', monospace", fontSize: "12px", color: "#1a1a2e",
          overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
        }}>
          {user.email}
        </span>
      </div>

      {/* Current Roles */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: "4px" }}>
        {user.roles.length > 0
          ? user.roles.map((r, i) => <RoleBadge key={r} role={r} index={i} />)
          : <span style={{ fontSize: "11px", color: "#bbb", fontFamily: "'DM Mono', monospace" }}>no roles</span>
        }
      </div>

      {/* Add Role */}
      <div style={{ display: "flex", gap: "6px", alignItems: "center" }}>
        {availableToAdd.length > 0 ? (
          <>
            <select value={addRole} onChange={(e) => setAddRole(e.target.value)} style={selectStyle}>
              {availableToAdd.map((r) => <option key={r} value={r}>{r}</option>)}
            </select>
            <button
              onClick={handleAdd}
              disabled={addLoading}
              onMouseEnter={() => setAddHover(true)}
              onMouseLeave={() => setAddHover(false)}
              style={{ ...btnBase("#38a169", addHover), opacity: addLoading ? 0.6 : 1 }}
            >
              {addLoading ? "…" : "Add"}
            </button>
          </>
        ) : (
          <span style={{ fontSize: "11px", color: "#bbb", fontFamily: "'DM Mono', monospace" }}>all assigned</span>
        )}
      </div>

      {/* Revoke Role */}
      <div style={{ display: "flex", gap: "6px", alignItems: "center" }}>
        {availableToRevoke.length > 0 ? (
          <>
            <select value={revokeRole} onChange={(e) => setRevokeRole(e.target.value)} style={selectStyle}>
              {availableToRevoke.map((r) => <option key={r} value={r}>{r}</option>)}
            </select>
            <button
              onClick={handleRevoke}
              disabled={revokeLoading}
              onMouseEnter={() => setRevokeHover(true)}
              onMouseLeave={() => setRevokeHover(false)}
              style={{ ...btnBase("#e94560", revokeHover), opacity: revokeLoading ? 0.6 : 1 }}
            >
              {revokeLoading ? "…" : "Revoke"}
            </button>
          </>
        ) : (
          <span style={{ fontSize: "11px", color: "#bbb", fontFamily: "'DM Mono', monospace" }}>no roles</span>
        )}
      </div>
    </div>
  );
}

export default function UsersPage() {
  const [users, setUsers] = useState([]);
  const [allRoles, setAllRoles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const token = useSelector((state) => state.auth.token);

  useEffect(() => {
    const headers = { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" };
    Promise.all([
      fetch(USERS_URL, { headers }).then((r) => { if (!r.ok) throw new Error(`Users: HTTP ${r.status}`); return r.json(); }),
      fetch(ROLES_URL, { headers }).then((r) => { if (!r.ok) throw new Error(`Roles: HTTP ${r.status}`); return r.json(); }),
    ])
      .then(([usersData, rolesData]) => {
        setUsers(usersData);
        // Normalize roles: handle both ["ADMIN"] and [{ name: "ADMIN" }]
        const normalized = rolesData.map((r) => (typeof r === "object" ? r.name : r));
        setAllRoles(normalized);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message);
        setLoading(false);
      });
  }, []);

  const handleUpdate = (updatedUser) => {
    setUsers((prev) => prev.map((u) => u.userId === updatedUser.userId ? updatedUser : u));
  };

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=DM+Mono:wght@300;400;500&family=Syne:wght@700;800&display=swap');
        * { box-sizing: border-box; margin: 0; padding: 0; }
        @keyframes fadeSlideIn {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes spin { to { transform: rotate(360deg); } }
        select:focus { border-color: #3b9ede !important; box-shadow: 0 0 0 2px rgba(59,158,222,0.15); }
      `}</style>

      <div style={{  background: "#ffffff", padding: "24px", fontFamily: "'DM Mono', monospace" }}>
        <div style={{ maxWidth: "1200px", margin: "0 auto" }}>
          <div style={{ background: "#ffffff", border: "1px solid #e8eaf0", borderRadius: "6px", overflow: "hidden" }}>

            {/* Table Header */}
            <div style={{
              display: "grid",
              gridTemplateColumns: "2fr 2fr 2fr 2fr",
              gap: "12px",
              padding: "12px 28px",
              background: "#f7f8fc",
              borderBottom: "1px solid #e8eaf0",
            }}>
              {["Email", "Roles", "Add Role", "Revoke Role"].map((h) => (
                <span key={h} style={{
                  fontSize: "10px", letterSpacing: "0.15em",
                  textTransform: "uppercase", color: "#99a",
                  fontFamily: "'DM Mono', monospace",
                }}>
                  {h}
                </span>
              ))}
            </div>

            {loading && (
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", padding: "64px 24px", gap: "16px" }}>
                <div style={{ width: "28px", height: "28px", border: "2px solid #e8eaf0", borderTop: "2px solid #e94560", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                <span style={{ fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>LOADING…</span>
              </div>
            )}

            {error && (
              <div style={{ display: "flex", flexDirection: "column", alignItems: "center", padding: "64px 24px", gap: "12px" }}>
                <span style={{ fontSize: "24px" }}>⚠</span>
                <span style={{ fontSize: "13px", color: "#e94560", fontFamily: "'DM Mono', monospace" }}>Failed to load data</span>
                <span style={{ fontSize: "11px", color: "#999", fontFamily: "'DM Mono', monospace" }}>{error}</span>
              </div>
            )}

            {!loading && !error && users.length === 0 && (
              <div style={{ padding: "64px 24px", textAlign: "center", fontSize: "12px", color: "#999", letterSpacing: "0.1em" }}>
                NO USERS FOUND
              </div>
            )}

            {!loading && !error && users.map((user, i) => (
              <UserRow
                key={user.userId}
                user={user}
                index={i}
                allRoles={allRoles}
                token={token}
                onUpdate={handleUpdate}
              />
            ))}
          </div>
        </div>
      </div>
    </>
  );
}