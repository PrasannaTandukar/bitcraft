import { useSelector } from "react-redux";
import { Navigate } from "react-router-dom";

export default function RoleRoute({ allowedRoles, redirectTo = "/", children }) {
  const { token, roles = [] } = useSelector((state) => state.auth);
  if (!token) return <Navigate to="/login" replace />;
  if (!allowedRoles.some((role) => roles.includes(role))) return <Navigate to={redirectTo} replace />;
  return children;
}
