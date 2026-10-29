import { Card, Descriptions, Tag, Typography } from "antd";
import { useSelector } from "react-redux";
import PageHeader from "../components/PageHeader.jsx";

export default function ProfilePage() {
    const { user, roles = [] } = useSelector((state) => state.auth);

    return (
        <main className="workspace-page">
            <div className="workspace-page-inner">
                <PageHeader title="Profile" description="Your account identity and assigned workspace roles." />
                <Card>
                    <Descriptions column={1} size="middle">
                        <Descriptions.Item label="Email">
                            <Typography.Text>{user || "Not available"}</Typography.Text>
                        </Descriptions.Item>
                        <Descriptions.Item label="Roles">
                            {roles.length
                                ? roles.map((role) => <Tag color="blue" key={role}>{role}</Tag>)
                                : <Typography.Text type="secondary">No roles assigned</Typography.Text>}
                        </Descriptions.Item>
                    </Descriptions>
                </Card>
            </div>
        </main>
    );
}
