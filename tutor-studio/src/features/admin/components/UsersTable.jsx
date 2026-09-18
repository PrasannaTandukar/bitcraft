import { SearchOutlined, SafetyCertificateOutlined } from "@ant-design/icons";
import { Avatar, Button, Empty, Flex, Input, Space, Table, Tag, Typography } from "antd";
import { useMemo, useState } from "react";
import { initial } from "../utils/formatting.js";

export default function UsersTable({ users, loading, onManageRoles }) {
  const [search, setSearch] = useState("");

  const filteredUsers = useMemo(() => {
    const query = search.trim().toLowerCase();
    if (!query) return users;
    return users.filter(
      (user) => user.email?.toLowerCase().includes(query) || user.roles.some((role) => role.toLowerCase().includes(query))
    );
  }, [search, users]);

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
      render: (roles) =>
        roles.length ? (
          <Flex gap={6} wrap>
            {roles.map((role) => (
              <Tag key={role}>{role}</Tag>
            ))}
          </Flex>
        ) : (
          <Typography.Text type="secondary">No roles assigned</Typography.Text>
        ),
    },
    {
      title: "",
      key: "action",
      width: 130,
      render: (_, user) => (
        <Button icon={<SafetyCertificateOutlined />} onClick={() => onManageRoles(user)}>
          Manage roles
        </Button>
      ),
    },
  ];

  return (
    <>
      <Input
        className="management-search"
        prefix={<SearchOutlined />}
        allowClear
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        placeholder="Search by email or role"
      />
      <Table
        className="workspace-table"
        rowKey="userId"
        columns={columns}
        dataSource={filteredUsers}
        loading={loading}
        pagination={{ pageSize: 10, showSizeChanger: true }}
        scroll={{ x: 680 }}
        locale={{ emptyText: <Empty description="No users found" /> }}
      />
    </>
  );
}
