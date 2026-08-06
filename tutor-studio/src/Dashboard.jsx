import Badge from 'react-bootstrap/Badge';
import Button from 'react-bootstrap/Button';
import Card from 'react-bootstrap/Card';
import Col from 'react-bootstrap/Col';
import Container from 'react-bootstrap/Container';
import Row from 'react-bootstrap/Row';
import { useNavigate } from 'react-router-dom';
import './Dashboard.css';

const stats = [
  {
    title: 'Documents',
    value: '0',
    description: 'Uploaded learning materials',
  },
  {
    title: 'Tutor Sessions',
    value: '0',
    description: 'AI-assisted study sessions',
  },
  {
    title: 'Benchmarks',
    value: '0',
    description: 'Model tests completed',
  },
  {
    title: 'Active Model',
    value: 'Ollama',
    description: 'Local LLM provider',
  },
];

const actions = [
  {
    title: 'Start Tutor Chat',
    description:
      'Ask questions, generate explanations, and interact with the local AI tutor.',
    buttonLabel: 'Open Tutor',
    route: '/ai-tutor',
  },
  {
    title: 'Upload Documents',
    description:
      'Add learning materials so the tutor can use them for context-aware answers.',
    buttonLabel: 'Manage Documents',
    route: '/files',
  },
  {
    title: 'Run Benchmark',
    description: 'Test model responses and compare AI tutor performance.',
    buttonLabel: 'Go to Benchmarking',
    route: '/benchmarking',
  },
];

const statuses = [
  { label: 'Backend API', value: 'Online' },
  { label: 'Database', value: 'Connected' },
  { label: 'Ollama', value: 'Local model provider' },
  { label: 'Authentication', value: 'Active' },
];

function Dashboard() {
  const navigate = useNavigate();

  return (
    <main className="dashboard-page">
      <Container className="dashboard-container">
        <section className="dashboard-welcome">
          <Badge className="dashboard-badge">Tutor Studio</Badge>
          <h1>Welcome back</h1>
          <p>
            Manage your AI tutor workspace, documents, and model tools from one
            place.
          </p>
        </section>

        <section aria-label="Workspace statistics">
          <Row className="g-3 g-xl-4">
            {stats.map((stat) => (
              <Col key={stat.title} sm={6} xl={3}>
                <Card className="dashboard-card stat-card">
                  <Card.Body>
                    <span>{stat.title}</span>
                    <strong>{stat.value}</strong>
                    <p>{stat.description}</p>
                  </Card.Body>
                </Card>
              </Col>
            ))}
          </Row>
        </section>

        <section className="dashboard-section" aria-labelledby="actions-title">
          <div className="section-heading">
            <h2 id="actions-title">Workspace actions</h2>
          </div>
          <Row className="g-3 g-xl-4">
            {actions.map((action) => (
              <Col key={action.title} lg={4}>
                <Card className="dashboard-card action-card">
                  <Card.Body>
                    <h3>{action.title}</h3>
                    <p>{action.description}</p>
                    <Button
                      className="dashboard-button"
                      onClick={() => navigate(action.route)}
                    >
                      {action.buttonLabel}
                    </Button>
                  </Card.Body>
                </Card>
              </Col>
            ))}
          </Row>
        </section>

        <Row className="dashboard-section g-3 g-xl-4">
          <Col lg={8}>
            <Card className="dashboard-card utility-card">
              <Card.Body>
                <h2>Recent Activity</h2>
                <div className="empty-state">
                  <strong>No recent activity yet.</strong>
                  <p>
                    Once you start using the tutor, your activity will appear
                    here.
                  </p>
                </div>
              </Card.Body>
            </Card>
          </Col>
          <Col lg={4}>
            <Card className="dashboard-card utility-card status-card">
              <Card.Body>
                <h2>System Status</h2>
                <ul>
                  {statuses.map((status) => (
                    <li key={status.label}>
                      <span>{status.label}</span>
                      <strong>{status.value}</strong>
                    </li>
                  ))}
                </ul>
              </Card.Body>
            </Card>
          </Col>
        </Row>
      </Container>
    </main>
  );
}

export default Dashboard;
