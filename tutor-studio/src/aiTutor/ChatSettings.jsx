import { Collapse, Flex, Slider, Switch, Typography } from "antd";

export default function ChatSettings({ topK, showSources, onTopKChange, onShowSourcesChange }) {
  return (
    <Collapse
      size="small"
      items={[{
        key: "settings",
        label: "Conversation settings",
        children: (
          <div className="runtime-settings-grid">
            <div>
              <Typography.Text type="secondary">Tutor knowledge</Typography.Text>
              <Typography.Text strong>Enabled by tutor configuration</Typography.Text>
            </div>
            <div>
              <Flex justify="space-between"><Typography.Text>Retrieval depth</Typography.Text><Typography.Text type="secondary">Top {topK}</Typography.Text></Flex>
              <Slider min={1} max={10} value={topK} onChange={onTopKChange} tooltip={{ formatter: (value) => `Top ${value}` }} />
            </div>
            <Flex align="center" justify="space-between" gap={16}>
              <Typography.Text>Show sources</Typography.Text>
              <Switch checked={showSources} onChange={onShowSourcesChange} />
            </Flex>
          </div>
        ),
      }]}
    />
  );
}
