import { Card, Descriptions, Empty, Space, Tag, Typography } from "antd";
import { useSelector } from "react-redux";
import PageHeader from "./components/PageHeader.jsx";

function Settings() {
  const { user, roles = [] } = useSelector((state) => state.auth);

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader title="Settings" description="Review the account and application preferences available to you." />

        <Space orientation="vertical" size={16} style={{ width: "100%" }}>
          <Card title="Account">
            <Descriptions column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="Email">{user || "Not available"}</Descriptions.Item>
              <Descriptions.Item label="Workspace roles">
                {roles.length ? roles.map((role) => <Tag key={role}>{role}</Tag>) : <Typography.Text type="secondary">No roles assigned</Typography.Text>}
              </Descriptions.Item>
            </Descriptions>
          </Card>

          <Card title="Application preferences">
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No configurable application preferences are currently exposed." />
          </Card>
        </Space>
      </div>
    </main>
  );
}

export default Settings;
