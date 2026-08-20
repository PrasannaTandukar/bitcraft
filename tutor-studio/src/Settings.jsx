import { BankOutlined, BookOutlined, ExperimentOutlined, FolderOutlined, RobotOutlined, SafetyCertificateOutlined, ThunderboltOutlined } from "@ant-design/icons";
import { Button, Card, Descriptions, Divider, Flex, Segmented, Space, Switch, Tag, Typography } from "antd";
import { useDispatch, useSelector } from "react-redux";
import { useNavigate } from "react-router-dom";
import PageHeader from "./components/PageHeader.jsx";
import { setTableDensity, setThemeMode } from "./store/preferencesSlice.js";

const QUICK_LINKS = [
  { path: "/admin/ai-providers", label: "Runtime engines", icon: <ThunderboltOutlined />, roles: ["Admin"] },
  { path: "/admin/ai-models", label: "AI models", icon: <RobotOutlined />, roles: ["Admin"] },
  { path: "/admin/embedding-models", label: "Embedding models", icon: <ExperimentOutlined />, roles: ["Admin"] },
  { path: "/admin/roles", label: "Roles", icon: <SafetyCertificateOutlined />, roles: ["Admin"] },
  { path: "/tutors", label: "Tutors", icon: <BankOutlined />, roles: ["Editor", "Auditor"] },
  { path: "/documents", label: "Documents", icon: <FolderOutlined />, roles: ["Editor", "Auditor"] },
  { path: "/benchmarking", label: "Benchmarking", icon: <BookOutlined />, roles: ["Editor", "Auditor"] },
];

const DENSITY_OPTIONS = [
  { label: "Compact", value: "small" },
  { label: "Comfortable", value: "middle" },
  { label: "Spacious", value: "large" },
];

function Settings() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { user, roles = [] } = useSelector((state) => state.auth);
  const { themeMode, tableDensity } = useSelector((state) => state.preferences);

  const quickLinks = QUICK_LINKS.filter((link) => link.roles.some((role) => roles.includes(role)));

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader title="Settings" description="Configure how Tutor Studio looks and behaves for your account." />

        <Space orientation="vertical" size={16} style={{ width: "100%" }}>
          <Card title="Appearance">
            <Flex align="center" justify="space-between" wrap gap={16}>
              <div>
                <Typography.Text strong>Dark mode</Typography.Text>
                <br />
                <Typography.Text type="secondary">Switch between light and dark theming across Tutor Studio.</Typography.Text>
              </div>
              <Switch
                checked={themeMode === "dark"}
                onChange={(checked) => dispatch(setThemeMode(checked ? "dark" : "light"))}
                checkedChildren="Dark"
                unCheckedChildren="Light"
              />
            </Flex>
          </Card>

          <Card title="Preferences">
            <Flex align="center" justify="space-between" wrap gap={16}>
              <div>
                <Typography.Text strong>Table density</Typography.Text>
                <br />
                <Typography.Text type="secondary">Controls the default size of tables and controls throughout the app.</Typography.Text>
              </div>
              <Segmented options={DENSITY_OPTIONS} value={tableDensity} onChange={(value) => dispatch(setTableDensity(value))} />
            </Flex>
          </Card>

          <Card title="Quick access">
            {quickLinks.length ? (
              <Flex gap={8} wrap>
                {quickLinks.map((link) => (
                  <Button key={link.path} icon={link.icon} onClick={() => navigate(link.path)}>{link.label}</Button>
                ))}
              </Flex>
            ) : (
              <Typography.Text type="secondary">No shortcuts are available for your current roles.</Typography.Text>
            )}
          </Card>

          <Card title="Account">
            <Descriptions column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="Email">{user || "Not available"}</Descriptions.Item>
              <Descriptions.Item label="Workspace roles">
                {roles.length ? roles.map((role) => <Tag key={role}>{role}</Tag>) : <Typography.Text type="secondary">No roles assigned</Typography.Text>}
              </Descriptions.Item>
            </Descriptions>
            <Divider style={{ margin: "12px 0" }} />
            <Typography.Text type="secondary">Visit the <Typography.Link onClick={() => navigate("/profile")}>Profile</Typography.Link> page to review your full account identity.</Typography.Text>
          </Card>
        </Space>
      </div>
    </main>
  );
}

export default Settings;
