import { Collapse, Flex, Tag, Typography } from "antd";

export default function SourceList({ sources }) {
  if (!sources?.length) return null;
  return (
    <Collapse
      ghost
      className="runtime-sources"
      items={[{
        key: "sources",
        label: `${sources.length} chunk${sources.length === 1 ? "" : "s"} retrieved`,
        children: (
          <div className="runtime-source-list">
            {sources.map((source) => (
              <Flex className="runtime-source-row" key={source.chunkId} justify="space-between" align="center" gap={12}>
                <div><Typography.Text strong>{source.documentName || "Tutor document"}</Typography.Text><br /><Typography.Text type="secondary">Chunk {source.chunkIndex}</Typography.Text></div>
                {Number.isFinite(source.score) ? <Tag>Cosine similarity {source.score.toFixed(3)}</Tag> : null}
              </Flex>
            ))}
          </div>
        ),
      }]}
    />
  );
}
