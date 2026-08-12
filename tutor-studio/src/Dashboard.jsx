import {
  DatabaseOutlined,
  ExperimentOutlined,
  FolderOpenOutlined,
  PlusOutlined,
  RobotOutlined,
} from "@ant-design/icons";
import { Alert, Button, Card, Col, Empty, Flex, Row, Skeleton, Statistic, Tag, Typography } from "antd";
import { useEffect, useMemo, useState } from "react";
import { useSelector } from "react-redux";
import { useNavigate } from "react-router-dom";
import PageHeader from "./components/PageHeader.jsx";
import { listDocuments } from "./documents/documentApi.js";
import { listTutors } from "./tutors/tutorApi.js";
import { formatDateTime, tutorStatus } from "./tutors/tutorUtils.js";
import StatusTag from "./components/StatusTag.jsx";
import "./Dashboard.css";

const { Text, Title } = Typography;

function Dashboard() {
  const navigate = useNavigate();
  const { roles = [] } = useSelector((state) => state.auth);
  const canViewWorkspaceData = roles.some((role) => ["Auditor", "Editor", "Admin"].includes(role));
  const canManage = roles.some((role) => ["Editor", "Admin"].includes(role));
  const [loading, setLoading] = useState(canViewWorkspaceData);
  const [error, setError] = useState("");
  const [tutors, setTutors] = useState([]);
  const [documents, setDocuments] = useState([]);

  useEffect(() => {
    if (!canViewWorkspaceData) return undefined;

    const controller = new AbortController();
    Promise.all([listTutors(controller.signal), listDocuments(controller.signal)])
      .then(([tutorItems, documentItems]) => {
        setTutors(tutorItems ?? []);
        setDocuments(documentItems ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError?.name !== "AbortError") {
          setError(requestError?.message || "Workspace data could not be loaded.");
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [canViewWorkspaceData]);

  const actions = useMemo(() => {
    const items = [
      {
        title: "Open AI Tutor",
        description: "Start a focused conversation with an available tutor.",
        label: "Open tutor",
        route: "/ai-tutor",
        icon: <RobotOutlined />,
      },
    ];

    if (canViewWorkspaceData) {
      items.push({
        title: "Browse knowledge",
        description: "Review uploaded documents and their vectorization state.",
        label: canManage ? "Manage files" : "View files",
        route: "/files",
        icon: <FolderOpenOutlined />,
      });
      items.push({
        title: "Evaluate tutors",
        description: "Configure benchmark cases and inspect evaluation runs.",
        label: "Open benchmarking",
        route: "/benchmarking",
        icon: <ExperimentOutlined />,
      });
    }

    return items;
  }, [canManage, canViewWorkspaceData]);

  return (
    <main className="workspace-page dashboard-page">
      <div className="workspace-page-inner">
        <PageHeader
          title="Dashboard"
          description="A concise view of the tutors and knowledge available in your workspace."
          extra={canManage ? <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate("/tutors")}>Create tutor</Button> : null}
        />

        {error ? <Alert className="dashboard-alert" type="error" title="Some workspace data is unavailable" description={error} showIcon /> : null}

        {canViewWorkspaceData ? (
          <Row gutter={[16, 16]}>
            <Col xs={24} sm={12}>
              <Card>
                <Skeleton loading={loading} active paragraph={false}>
                  <Statistic title="Tutors" value={tutors.length} prefix={<RobotOutlined />} />
                </Skeleton>
              </Card>
            </Col>
            <Col xs={24} sm={12}>
              <Card>
                <Skeleton loading={loading} active paragraph={false}>
                  <Statistic title="Knowledge documents" value={documents.length} prefix={<DatabaseOutlined />} />
                </Skeleton>
              </Card>
            </Col>
          </Row>
        ) : null}

        <section className="workspace-section">
          <Title className="workspace-section-heading" level={4}>Quick actions</Title>
          <Row gutter={[16, 16]}>
            {actions.map((action) => (
              <Col key={action.route} xs={24} md={12} xl={8}>
                <Card className="dashboard-action-card">
                  <span className="dashboard-action-icon">{action.icon}</span>
                  <Title level={5}>{action.title}</Title>
                  <Text type="secondary">{action.description}</Text>
                  <Button onClick={() => navigate(action.route)}>{action.label}</Button>
                </Card>
              </Col>
            ))}
          </Row>
        </section>

        {canViewWorkspaceData ? (
          <section className="workspace-section">
            <Card title="Available tutors" extra={<Button type="link" onClick={() => navigate("/tutors")}>View all</Button>}>
              <Skeleton loading={loading} active>
                {tutors.length ? (
                  <div className="dashboard-tutor-list">
                    {tutors.slice(0, 5).map((tutor) => (
                      <Flex className="dashboard-tutor-row" key={tutor.id} align="center" gap={12}>
                        <span className="dashboard-list-icon"><RobotOutlined /></span>
                        <div className="dashboard-tutor-copy">
                          <Typography.Text strong>{tutor.name}</Typography.Text>
                          <Typography.Text type="secondary" ellipsis={{ tooltip: tutor.description }}>{tutor.description || `Updated ${formatDateTime(tutor.updatedAt)}`}</Typography.Text>
                        </div>
                        <StatusTag value={tutorStatus(tutor.status)} />
                        <Button type="link" onClick={() => navigate(`/tutors/${tutor.id}`)}>Open</Button>
                      </Flex>
                    ))}
                  </div>
                ) : (
                  <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No tutors are available yet">
                    {canManage ? <Button type="primary" onClick={() => navigate("/tutors")}>Create a tutor</Button> : null}
                  </Empty>
                )}
              </Skeleton>
            </Card>
          </section>
        ) : (
          <Alert
            className="workspace-section"
            type="info"
            showIcon
            title="Workspace access is limited"
            description="Your account can use the AI Tutor. Additional management areas appear when a workspace role is assigned."
          />
        )}
      </div>
    </main>
  );
}

export default Dashboard;
