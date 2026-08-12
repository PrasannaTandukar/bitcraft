import { SafetyCertificateOutlined, SearchOutlined } from "@ant-design/icons";
import { Alert, App, Avatar, Button, Drawer, Empty, Flex, Input, Popconfirm, Select, Space, Table, Tag, Typography } from "antd";
import { useEffect, useMemo, useState } from "react";
import { apiFetch, getApiErrorDescription } from "./api/client.js";
import PageHeader from "./components/PageHeader.jsx";

const USERS_PATH = "/api/admin/users";
const ROLES_PATH = "/api/admin/roles";

function initial(value) {
  return value?.trim()?.[0]?.toUpperCase() || "U";
}

export default function UsersPage() {
  const { message } = App.useApp();
  const [users, setUsers] = useState([]);
  const [allRoles, setAllRoles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [selectedUserId, setSelectedUserId] = useState(null);
  const [roleToAdd, setRoleToAdd] = useState();
  const [busy, setBusy] = useState("");
  const [manageError, setManageError] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([apiFetch(USERS_PATH, { signal: controller.signal }), apiFetch(ROLES_PATH, { signal: controller.signal })])
      .then(([usersData, rolesData]) => {
        setUsers(usersData ?? []);
        setAllRoles((rolesData ?? []).map((role) => typeof role === "object" ? role.name : role).filter(Boolean));
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Role management data could not be loaded."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, []);

  const selectedUser = users.find((user) => user.userId === selectedUserId) || null;
  const availableRoles = selectedUser ? allRoles.filter((role) => !selectedUser.roles.includes(role)) : [];
  const filteredUsers = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return users;
    return users.filter((user) => user.email?.toLowerCase().includes(query) || user.roles.some((role) => role.toLowerCase().includes(query)));
  }, [search, users]);

  const updateUser = (userId, updater) => {
    setUsers((current) => current.map((user) => user.userId === userId ? updater(user) : user));
  };

  const addRole = async () => {
    if (!selectedUser || !roleToAdd || busy) return;
    setBusy(`add:${roleToAdd}`);
    setManageError("");
    try {
      await apiFetch(`${USERS_PATH}/${selectedUser.userId}/roles`, { method: "POST", json: { role: roleToAdd }, parse: "none" });
      updateUser(selectedUser.userId, (user) => ({ ...user, roles: [...user.roles, roleToAdd] }));
      message.success(`${roleToAdd} role assigned.`);
      setRoleToAdd(undefined);
    } catch (requestError) {
      setManageError(getApiErrorDescription(requestError, "The role could not be assigned."));
    } finally {
      setBusy("");
    }
  };

  const revokeRole = async (role) => {
    if (!selectedUser || busy) return;
    setBusy(`revoke:${role}`);
    setManageError("");
    try {
      await apiFetch(`${USERS_PATH}/${selectedUser.userId}/roles/${encodeURIComponent(role)}`, { method: "DELETE", parse: "none" });
      updateUser(selectedUser.userId, (user) => ({ ...user, roles: user.roles.filter((item) => item !== role) }));
      message.success(`${role} role revoked.`);
    } catch (requestError) {
      setManageError(getApiErrorDescription(requestError, "The role could not be revoked."));
      throw requestError;
    } finally {
      setBusy("");
    }
  };

  const columns = [
    {
      title: "User",
      key: "user",
      sorter: (a, b) => a.email.localeCompare(b.email),
      render: (_, user) => (
        <Space>
          <Avatar>{initial(user.email)}</Avatar>
          <Typography.Text ellipsis={{ tooltip: user.email }}>{user.email}</Typography.Text>
        </Space>
      ),
    },
    {
      title: "Roles",
      dataIndex: "roles",
      render: (roles) => roles.length ? <Flex gap={6} wrap>{roles.map((role) => <Tag key={role}>{role}</Tag>)}</Flex> : <Typography.Text type="secondary">No roles assigned</Typography.Text>,
    },
    {
      title: "",
      key: "action",
      width: 130,
      render: (_, user) => <Button icon={<SafetyCertificateOutlined />} onClick={() => { setSelectedUserId(user.userId); setRoleToAdd(undefined); setManageError(""); }}>Manage roles</Button>,
    },
  ];

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader title="Role management" description="Assign and revoke the existing workspace roles available through ASP.NET Core Identity." />
        {error ? <Alert type="error" title="Users and roles could not be loaded" description={error} showIcon style={{ marginBottom: 16 }} /> : null}
        <Input className="management-search" prefix={<SearchOutlined />} allowClear value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by email or role" />
        <Table className="workspace-table" rowKey="userId" columns={columns} dataSource={filteredUsers} loading={loading} pagination={{ pageSize: 10, showSizeChanger: true }} scroll={{ x: 680 }} locale={{ emptyText: <Empty description="No users found" /> }} />
      </div>

      <Drawer title="Manage roles" open={Boolean(selectedUser)} onClose={() => setSelectedUserId(null)} size={520}>
        {selectedUser ? (
          <Space orientation="vertical" size={20} style={{ width: "100%" }}>
            <Space><Avatar size="large">{initial(selectedUser.email)}</Avatar><div><Typography.Text strong>{selectedUser.email}</Typography.Text><br /><Typography.Text type="secondary">Workspace access</Typography.Text></div></Space>
            {manageError ? <Alert type="error" title={manageError} showIcon /> : null}
            <div>
              <Typography.Title level={5}>Assign role</Typography.Title>
              <Flex gap={8}>
                <Select value={roleToAdd} onChange={setRoleToAdd} placeholder={availableRoles.length ? "Select a role" : "All roles assigned"} disabled={!availableRoles.length || Boolean(busy)} style={{ flex: 1 }} options={availableRoles.map((role) => ({ value: role, label: role }))} />
                <Button type="primary" onClick={addRole} loading={busy.startsWith("add:")} disabled={!roleToAdd}>Assign</Button>
              </Flex>
            </div>
            <div>
              <Typography.Title level={5}>Assigned roles</Typography.Title>
              {selectedUser.roles.length ? (
                <div className="management-resource-list">
                  {selectedUser.roles.map((role) => (
                    <Flex className="management-resource-row" key={role} align="center" justify="space-between" gap={12}>
                      <Tag>{role}</Tag>
                      <Popconfirm title={`Revoke ${role}?`} description="The user's access changes immediately after the backend accepts this action." okText="Revoke role" okButtonProps={{ danger: true }} onConfirm={() => revokeRole(role)}><Button danger size="small" loading={busy === `revoke:${role}`}>Revoke</Button></Popconfirm>
                    </Flex>
                  ))}
                </div>
              ) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No roles assigned" />}
            </div>
          </Space>
        ) : null}
      </Drawer>
    </main>
  );
}
