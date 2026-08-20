import {
  BookOutlined,
  DatabaseOutlined,
  ExperimentOutlined,
  RobotOutlined,
} from "@ant-design/icons";
import { Flex, Typography } from "antd";

const { Paragraph, Text, Title } = Typography;

const capabilities = [
  { icon: <RobotOutlined />, text: "Configure and operate AI tutors" },
  { icon: <DatabaseOutlined />, text: "Curate connected knowledge" },
  { icon: <ExperimentOutlined />, text: "Evaluate tutor quality" },
];

function AuthShell({ title, description, children }) {
  return (
    <main className="auth-page">
      <section className="auth-layout">
        <aside className="auth-introduction">
          <Flex className="auth-brand" align="center" gap={12}>
            <span className="auth-brand-mark"><BookOutlined /></span>
            <span>
              <Text strong>Bitcraft</Text>
              <Text type="secondary">Tutor Studio</Text>
            </span>
          </Flex>

          <div className="auth-product-copy">
            <Title level={1}>Build reliable AI tutoring experiences.</Title>
            <Paragraph>
              One focused workspace for tutors, knowledge, models, runtimes, and evaluation.
            </Paragraph>
          </div>

          <div className="auth-capabilities">
            {capabilities.map((capability) => (
              <Flex key={capability.text} align="center" gap={12}>
                <span className="auth-capability-icon">{capability.icon}</span>
                <Text>{capability.text}</Text>
              </Flex>
            ))}
          </div>
        </aside>

        <section className="auth-form-panel" aria-label={title}>
          <div className="auth-form-content">
            <Title level={2}>{title}</Title>
            <Paragraph type="secondary">{description}</Paragraph>
            {children}
          </div>
        </section>
      </section>
    </main>
  );
}

export default AuthShell;
