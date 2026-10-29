import { Flex, Typography } from "antd";

const { Paragraph, Title } = Typography;

function PageHeader({ title, description, extra }) {
  return (
    <Flex className="workspace-page-header" justify="space-between" align="flex-start" gap={16} wrap>
      <div>
        <Title level={2}>{title}</Title>
        {description ? <Paragraph type="secondary">{description}</Paragraph> : null}
      </div>
      {extra ? <Flex gap={8} wrap>{extra}</Flex> : null}
    </Flex>
  );
}

export default PageHeader;
