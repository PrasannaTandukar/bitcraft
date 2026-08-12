import { LogoutOutlined, RobotOutlined } from "@ant-design/icons";
import { Avatar, Button, Divider, Menu, Tooltip, Typography } from "antd";
import { useLocation, useNavigate } from "react-router-dom";
import { getAccountDisplay } from "./accountDisplay.js";
import {
  buildNavigationItems,
  getPageMetadata,
  utilityNavigationItems,
} from "./navigationConfig.jsx";

export default function AppSidebar({
  collapsed = false,
  mobile = false,
  roles = [],
  user,
  onNavigate,
  onSignOut,
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const account = getAccountDisplay(user, roles);
  const selectedKey = getPageMetadata(location.pathname).key;

  const navigateTo = ({ key }) => {
    navigate(key);
    onNavigate?.();
  };

  return (
    <div className="bitcraft-sidebar-content">
      <Button
        type="text"
        className={`bitcraft-brand ${collapsed && !mobile ? "is-collapsed" : ""}`}
        onClick={() => {
          navigate("/");
          onNavigate?.();
        }}
        aria-label="Go to dashboard"
      >
        <span className="bitcraft-brand-mark" aria-hidden="true">
          <RobotOutlined />
        </span>
        {(!collapsed || mobile) && (
          <span className="bitcraft-brand-copy">
            <strong>Bitcraft</strong>
            <small>Tutor Studio</small>
          </span>
        )}
      </Button>

      <nav className="bitcraft-sidebar-navigation" aria-label="Primary navigation">
        <Menu
          mode="inline"
          selectedKeys={selectedKey ? [selectedKey] : []}
          items={buildNavigationItems(roles)}
          onClick={navigateTo}
        />
      </nav>

      <div className="bitcraft-sidebar-footer">
        <Divider />
        <Menu
          mode="inline"
          selectedKeys={selectedKey === "/settings" ? [selectedKey] : []}
          items={utilityNavigationItems}
          onClick={navigateTo}
        />

        <Tooltip title={collapsed && !mobile ? account.displayName : undefined} placement="right">
          <div className={`bitcraft-sidebar-account ${collapsed && !mobile ? "is-collapsed" : ""}`}>
            <Avatar size={36}>{account.initials}</Avatar>
            {(!collapsed || mobile) && (
              <span className="bitcraft-account-copy">
                <Typography.Text ellipsis>{account.displayName}</Typography.Text>
                <Typography.Text type="secondary">{account.role}</Typography.Text>
              </span>
            )}
          </div>
        </Tooltip>

        <Menu
          mode="inline"
          selectable={false}
          items={[{
            key: "sign-out",
            label: "Sign out",
            icon: <LogoutOutlined />,
            danger: true,
          }]}
          onClick={onSignOut}
        />
      </div>
    </div>
  );
}
