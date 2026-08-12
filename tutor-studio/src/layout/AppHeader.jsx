import {
  DownOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuOutlined,
  MenuUnfoldOutlined,
  SettingOutlined,
} from "@ant-design/icons";
import { Avatar, Button, Dropdown, Layout, Typography } from "antd";
import { useLocation, useNavigate } from "react-router-dom";
import { getAccountDisplay } from "./accountDisplay.js";
import { getPageMetadata } from "./navigationConfig.jsx";

const { Header } = Layout;

export default function AppHeader({
  collapsed,
  isMobile,
  roles = [],
  user,
  onMenuClick,
  onCollapse,
  onSignOut,
}) {
  const location = useLocation();
  const navigate = useNavigate();
  const account = getAccountDisplay(user, roles);
  const page = getPageMetadata(location.pathname);

  const accountMenu = {
    items: [
      { key: "settings", label: "Settings", icon: <SettingOutlined /> },
      { type: "divider" },
      { key: "sign-out", label: "Sign out", icon: <LogoutOutlined />, danger: true },
    ],
    onClick: ({ key }) => {
      if (key === "settings") navigate("/settings");
      if (key === "sign-out") onSignOut();
    },
  };

  return (
    <Header className="bitcraft-header">
      <div className="bitcraft-header-leading">
        <Button
          type="text"
          className="bitcraft-navigation-trigger"
          icon={isMobile
            ? <MenuOutlined />
            : collapsed
              ? <MenuUnfoldOutlined />
              : <MenuFoldOutlined />}
          onClick={isMobile ? onMenuClick : onCollapse}
          aria-label={isMobile ? "Open navigation" : collapsed ? "Expand navigation" : "Collapse navigation"}
        />
        <Typography.Title level={5}>{page.title}</Typography.Title>
      </div>

      <Dropdown menu={accountMenu} trigger={["click"]} placement="bottomRight">
        <Button type="text" className="bitcraft-account-trigger" aria-label="Open account menu">
          <Avatar size={34}>{account.initials}</Avatar>
          <span className="bitcraft-header-account-copy">
            <Typography.Text ellipsis>{account.displayName}</Typography.Text>
            <Typography.Text type="secondary">{account.role}</Typography.Text>
          </span>
          <DownOutlined className="bitcraft-account-chevron" />
        </Button>
      </Dropdown>
    </Header>
  );
}
