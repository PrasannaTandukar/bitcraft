import { QuestionCircleOutlined } from "@ant-design/icons";
import { Tooltip, Typography } from "antd";

/**
 * Reusable help tooltip for dense or derived values.
 *
 * Use this instead of a bare `title` attribute so the hint is keyboard
 * reachable and screen-reader announced.
 */
export default function InfoTooltip({ title, text, children }) {
  if (!text && !title) return children ?? null;

  const content = (
    <>
      {title ? <div><Typography.Text strong>{title}</Typography.Text></div> : null}
      {text ? <div>{text}</div> : null}
    </>
  );

  return (
    <Tooltip title={content}>
      <span className="info-tooltip">
        {children}
        <QuestionCircleOutlined aria-label={title || text} style={{ marginLeft: 4, opacity: 0.65 }} />
      </span>
    </Tooltip>
  );
}
