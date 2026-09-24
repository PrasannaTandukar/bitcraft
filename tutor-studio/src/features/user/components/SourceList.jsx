import { Collapse, Flex, Tag, Typography } from "antd";
import { sourcePercent } from "../utils/aiTutorUtils.js";

export default function SourceList({ sources }) {
  if (!sources?.length) return null;
  return (
    <Collapse
      ghost
      className="runtime-sources"
      items={[{
        key: "sources",
        label: `${sources.length} source${sources.length === 1 ? "" : "s"} used`,
        children: (
          <div className="runtime-source-list">
            {sources.map((source) => (
              <Flex className="runtime-source-row" key={source.chunkId} justify="space-between" align="center" gap={12}>
                <div><Typography.Text strong>{source.documentName || "Tutor document"}</Typography.Text><br /><Typography.Text type="secondary">Chunk {source.chunkIndex}</Typography.Text></div>
                <Tag>Similarity {sourcePercent(source.score)}</Tag>
              </Flex>
            ))}
          </div>
        ),
      }]}
    />
  );
}
