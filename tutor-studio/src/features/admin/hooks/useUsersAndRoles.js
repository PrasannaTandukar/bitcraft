import { useEffect, useState, useCallback } from "react";
import { apiFetch, getApiErrorDescription } from "../../../api/client.js";

const USERS_PATH = "/api/admin/users";
const ROLES_PATH = "/api/admin/roles";
const REGISTER_PATH = "/api/Auth/register";

export function useUsersAndRoles() {
  const [users, setUsers] = useState([]);
  const [allRoles, setAllRoles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState("");
  const [manageError, setManageError] = useState("");
  const [registering, setRegistering] = useState(false);
  const [registerError, setRegisterError] = useState("");

  const fetchUsers = useCallback((signal) => {
    return Promise.all([
      apiFetch(USERS_PATH, { signal }),
      apiFetch(ROLES_PATH, { signal }),
    ]).then(([usersData, rolesData]) => {
      setUsers(usersData ?? []);
      setAllRoles(
        (rolesData ?? [])
          .map((role) => (typeof role === "object" ? role.name : role))
          .filter(Boolean),
      );
      setError("");
    });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    fetchUsers(controller.signal)
      .catch((requestError) => {
        if (requestError.name !== "AbortError")
          setError(
            getApiErrorDescription(
              requestError,
              "Role management data could not be loaded.",
            ),
          );
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [fetchUsers]);

  const updateUser = (userId, updater) => {
    setUsers((current) =>
      current.map((user) => (user.userId === userId ? updater(user) : user)),
    );
  };

  const addRole = async (userId, role) => {
    if (!role || busy) return;
    setBusy(`add:${role}`);
    setManageError("");
    try {
      await apiFetch(`${USERS_PATH}/${userId}/roles`, {
        method: "POST",
        json: { role },
        parse: "none",
      });
      updateUser(userId, (user) => ({ ...user, roles: [...user.roles, role] }));
      return true;
    } catch (requestError) {
      setManageError(
        getApiErrorDescription(requestError, "The role could not be assigned."),
      );
      return false;
    } finally {
      setBusy("");
    }
  };

  const revokeRole = async (userId, role) => {
    if (busy) return;
    setBusy(`revoke:${role}`);
    setManageError("");
    try {
      await apiFetch(
        `${USERS_PATH}/${userId}/roles/${encodeURIComponent(role)}`,
        { method: "DELETE", parse: "none" },
      );
      updateUser(userId, (user) => ({
        ...user,
        roles: user.roles.filter((item) => item !== role),
      }));
    } catch (requestError) {
      setManageError(
        getApiErrorDescription(requestError, "The role could not be revoked."),
      );
      throw requestError;
    } finally {
      setBusy("");
    }
  };

  const registerUser = async ({ email, password }) => {
    setRegistering(true);
    setRegisterError("");
    try {
      await apiFetch(REGISTER_PATH, {
        method: "POST",
        json: { email, password },
        parse: "none",
      });
      await fetchUsers();
      return true;
    } catch (requestError) {
      setRegisterError(
        getApiErrorDescription(
          requestError,
          "The user could not be registered.",
        ),
      );
      return false;
    } finally {
      setRegistering(false);
    }
  };

  return {
    users,
    allRoles,
    loading,
    error,
    busy,
    manageError,
    setManageError,
    registering,
    registerError,
    setRegisterError,
    addRole,
    revokeRole,
    registerUser,
  };
}
