import { Drawer, Grid, Layout } from "antd";
import { useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Outlet, useNavigate } from "react-router-dom";
import { logout } from "../store/authSlice.js";
import AppHeader from "./AppHeader.jsx";
import AppSidebar from "./AppSidebar.jsx";

const { Content, Sider } = Layout;

export default function AppLayout() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const screens = Grid.useBreakpoint();
  const { user, roles = [] } = useSelector((state) => state.auth);
  const [collapsed, setCollapsed] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const isMobile = screens.lg === false;

  const signOut = () => {
    dispatch(logout());
    navigate("/login", { replace: true });
  };

  const sidebar = (
    <AppSidebar
      collapsed={collapsed}
      mobile={isMobile}
      roles={roles}
      user={user}
      onNavigate={() => setDrawerOpen(false)}
      onSignOut={signOut}
    />
  );

  return (
    <Layout className="bitcraft-shell">
      {!isMobile && (
        <Sider
          className="bitcraft-sider"
          width={240}
          collapsedWidth={72}
          collapsed={collapsed}
          trigger={null}
          theme="light"
        >
          {sidebar}
        </Sider>
      )}

      <Drawer
        rootClassName="bitcraft-mobile-drawer"
        open={isMobile && drawerOpen}
        onClose={() => setDrawerOpen(false)}
        placement="left"
        size={280}
        title={null}
        styles={{ body: { padding: 0 } }}
      >
        {isMobile && sidebar}
      </Drawer>

      <Layout className="bitcraft-main-layout">
        <AppHeader
          collapsed={collapsed}
          isMobile={isMobile}
          roles={roles}
          user={user}
          onMenuClick={() => setDrawerOpen(true)}
          onCollapse={() => setCollapsed((value) => !value)}
          onSignOut={signOut}
        />
        <Content className="bitcraft-content">
          <div className="bitcraft-content-inner">
            <Outlet />
          </div>
        </Content>
      </Layout>
    </Layout>
  );
}
