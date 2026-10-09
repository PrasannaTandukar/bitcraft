import { Alert, Card, Typography } from "antd";

/**
 * Reusable help panel for the Benchmarking workflow.
 *
 * Copy follows Simplified Technical English: active voice, one instruction per
 * sentence, and no semicolons.
 */
export default function BenchmarkingHelp({ title, steps, note }) {
  if (!steps?.length) return null;

  return (
    <Card size="small" title={title} style={{ marginBottom: 16 }}>
      <ol style={{ margin: 0, paddingLeft: 20 }}>
        {steps.map((step, index) => (
          <li key={index} style={{ marginBottom: 4 }}>
            <Typography.Text>{step}</Typography.Text>
          </li>
        ))}
      </ol>
      {note ? <Alert type="info" title={note} showIcon style={{ marginTop: 12 }} /> : null}
    </Card>
  );
}
