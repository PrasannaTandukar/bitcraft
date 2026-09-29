import {
  Alert,
  Avatar,
  Button,
  Drawer,
  Empty,
  Flex,
  Popconfirm,
  Select,
  Space,
  Tag,
  Typography,
} from "antd";
import { useState } from "react";
import { initial } from "../utils/formatting.js";

export default function RoleManagerDrawer({
  user,
  allRoles,
  busy,
  manageError,
  onAssign,
  onRevoke,
  onClose,
}) {
  const [roleToAdd, setRoleToAdd] = useState();
  const availableRoles = user
    ? allRoles.filter((role) => !user.roles.includes(role))
    : [];

  const handleAssign = async () => {
    const success = await onAssign(user.userId, roleToAdd);
    if (success) setRoleToAdd(undefined);
  };

  return (
    <Drawer
      title="Manage roles"
      open={Boolean(user)}
      onClose={onClose}
      size={520}
    >
      {user ? (
        <Space orientation="vertical" size={20} style={{ width: "100%" }}>
          <Space>
            <Avatar size="large">{initial(user.email)}</Avatar>
            <div>
              <Typography.Text strong>{user.email}</Typography.Text>
              <br />
              <Typography.Text type="secondary">
                Workspace access
              </Typography.Text>
            </div>
          </Space>
          {manageError ? (
            <Alert type="error" title={manageError} showIcon />
          ) : null}
          <div>
            <Typography.Title level={5}>Assign role</Typography.Title>
            <Flex gap={8}>
              <Select
                value={roleToAdd}
                onChange={setRoleToAdd}
                placeholder={
                  availableRoles.length ? "Select a role" : "All roles assigned"
                }
                disabled={!availableRoles.length || Boolean(busy)}
                style={{ flex: 1 }}
                options={availableRoles.map((role) => ({
                  value: role,
                  label: role,
                }))}
              />
              <Button
                type="primary"
                onClick={handleAssign}
                loading={busy.startsWith("add:")}
                disabled={!roleToAdd}
              >
                Assign
              </Button>
            </Flex>
          </div>
          <div>
            <Typography.Title level={5}>Assigned roles</Typography.Title>
            {user.roles.length ? (
              <div className="management-resource-list">
                {user.roles.map((role) => (
                  <Flex
                    className="management-resource-row"
                    key={role}
                    align="center"
                    justify="space-between"
                    gap={12}
                  >
                    <Tag>{role}</Tag>
                    <Popconfirm
                      title={`Revoke ${role}?`}
                      description="The user's access changes immediately after the backend accepts this action."
                      okText="Revoke role"
                      okButtonProps={{ danger: true }}
                      onConfirm={() => onRevoke(user.userId, role)}
                    >
                      <Button
                        danger
                        size="small"
                        loading={busy === `revoke:${role}`}
                      >
                        Revoke
                      </Button>
                    </Popconfirm>
                  </Flex>
                ))}
              </div>
            ) : (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="No roles assigned"
              />
            )}
          </div>
        </Space>
      ) : null}
    </Drawer>
  );
}
