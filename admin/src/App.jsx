import './App.css'

const modules = [
  {
    title: 'Knowledge Base',
    description: 'Manage documents, chunks, and semantic search content.',
  },
  {
    title: 'System Health',
    description: 'Review API, database, and Ollama service availability.',
  },
  {
    title: 'Settings',
    description: 'Configure models, environments, and operational defaults.',
  },
]

function App() {
  return (
    <main className="admin-shell">
      <section className="hero-panel">
        <div>
          <p className="eyebrow">Bitcraft</p>
          <h1>Admin</h1>
          <p className="lead">
            Internal control panel for knowledge ingestion, search operations,
            and platform monitoring.
          </p>
        </div>

        <div className="status-card">
          <span className="status-dot" aria-hidden="true" />
          <div>
            <strong>Environment ready</strong>
            <p>Backend, database, and admin client are wired for development.</p>
          </div>
        </div>
      </section>

      <section className="stats-grid" aria-label="Admin overview">
        <article>
          <span>Modules</span>
          <strong>3</strong>
        </article>
        <article>
          <span>Primary API</span>
          <strong>:8000</strong>
        </article>
        <article>
          <span>Admin UI</span>
          <strong>:5173</strong>
        </article>
      </section>

      <section className="module-grid">
        {modules.map((module) => (
          <article key={module.title} className="module-card">
            <h2>{module.title}</h2>
            <p>{module.description}</p>
          </article>
        ))}
      </section>
    </main>
  )
}

export default App
