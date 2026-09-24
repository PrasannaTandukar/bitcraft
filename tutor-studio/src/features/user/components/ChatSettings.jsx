import { Flex, Slider, Switch, Typography } from "antd";

export default function ChatSettings({ topK, showSources, useMemory, saveToMemory,
  onTopKChange, onShowSourcesChange, onUseMemoryChange, onSaveToMemoryChange }) {
  return (
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
        <Switch aria-label="Show sources" checked={showSources} onChange={onShowSourcesChange} />
      </Flex>
      <Flex align="center" justify="space-between" gap={16}>
        <div>
          <Typography.Text>Use Tutor Memory</Typography.Text>
          <Typography.Paragraph type="secondary">Use relevant information you previously chose to save with this tutor.</Typography.Paragraph>
        </div>
        <Switch aria-label="Use Tutor Memory" checked={useMemory} onChange={onUseMemoryChange} />
      </Flex>
      <Flex align="center" justify="space-between" gap={16}>
        <div>
          <Typography.Text>Save this conversation to memory</Typography.Text>
          <Typography.Paragraph type="secondary">Save this question and answer for future conversations with this tutor.</Typography.Paragraph>
        </div>
        <Switch aria-label="Save this conversation to memory" checked={saveToMemory} onChange={onSaveToMemoryChange} />
      </Flex>
    </div>
  );
}
