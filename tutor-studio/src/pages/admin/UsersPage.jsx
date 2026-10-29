import { PlusOutlined } from "@ant-design/icons";
import { Alert, Button, Flex } from "antd";
import { useState } from "react";
import { useUsersAndRoles } from "../../features/admin/hooks/useUsersAndRoles.js";
import UsersTable from "../../features/admin/components/UsersTable.jsx";
import RoleManagerDrawer from "../../features/admin/components/RoleManagerDrawer.jsx";
import AddUserDrawer from "../../features/admin/components/AddUserDrawer.jsx";
import PageHeader from "../../components/PageHeader.jsx";

export default function UsersPage() {
  const {
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
  } = useUsersAndRoles();

  const [selectedUserId, setSelectedUserId] = useState(null);
  const [addUserOpen, setAddUserOpen] = useState(false);
  const selectedUser = users.find((u) => u.userId === selectedUserId) || null;

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader
          title="Role management"
          description="Assign and revoke the existing workspace roles available through ASP.NET Core Identity."
          extra={[
            <Button
              key="add-user"
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => {
                setRegisterError("");
                setAddUserOpen(true);
              }}
            >
              Add user
            </Button>,
          ]}
        />
        {error ? (
          <Alert
            type="error"
            title="Users and roles could not be loaded"
            description={error}
            showIcon
            style={{ marginBottom: 16 }}
          />
        ) : null}
        <UsersTable
          users={users}
          loading={loading}
          onManageRoles={(user) => {
            setSelectedUserId(user.userId);
            setManageError("");
          }}
        />
      </div>

      <RoleManagerDrawer
        user={selectedUser}
        allRoles={allRoles}
        busy={busy}
        manageError={manageError}
        onAssign={addRole}
        onRevoke={revokeRole}
        onClose={() => setSelectedUserId(null)}
      />

      <AddUserDrawer
        open={addUserOpen}
        saving={registering}
        registerError={registerError}
        onSubmit={registerUser}
        onClose={() => setAddUserOpen(false)}
      />
    </main>
  );
}
