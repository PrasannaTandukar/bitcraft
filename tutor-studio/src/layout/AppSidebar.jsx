import { Button, Menu } from "antd";
import { useLocation, useNavigate } from "react-router-dom";
import {
  buildNavigationItems,
  getPageMetadata,
} from "./navigationConfig.jsx";

export default function AppSidebar({
  collapsed = false,
  mobile = false,
  roles = [],
  onNavigate,
}) {
  const navigate = useNavigate();
  const location = useLocation();
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
          <img src="/favicon.svg" alt="Bitcraft Logo" className="bitcraft-brand-logo" />
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

    </div>
  );
}
