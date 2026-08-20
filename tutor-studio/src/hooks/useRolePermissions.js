import { useSelector } from "react-redux";

// Centralized CRUD permission mapping for Tutor Studio.
// Admin: full CRUD. Editor: Create/Read/Update only (no Delete). Everyone else (e.g. Auditor): Read only.
export default function useRolePermissions() {
    const { roles = [] } = useSelector((state) => state.auth);
    const isAdmin = roles.includes("Admin");
    const isEditor = roles.includes("Editor");
    const canWrite = isAdmin || isEditor; // create + update
    const canDelete = isAdmin; // destructive operations are Admin-only

    return { roles, isAdmin, isEditor, canWrite, canDelete };
}
