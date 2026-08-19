import {
  ApiOutlined,
  DashboardOutlined,
  DatabaseOutlined,
  DeploymentUnitOutlined,
  ExperimentOutlined,
  FileTextOutlined,
  FolderOpenOutlined,
  RobotOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
  UploadOutlined,
} from "@ant-design/icons";

export const standaloneNavigationItems = [
  { key: "/", label: "Dashboard", icon: <DashboardOutlined /> },
  { key: "/chat", label: "Chat", icon: <RobotOutlined /> },
];

export const navigationGroups = [
  {
    key: "documents",
    label: "Documents",
    items: [
      {
        key: "/documents",
        label: "Browse",
        icon: <FolderOpenOutlined />,
        roles: ["Auditor", "Editor", "Admin"],
      },
      {
        key: "/documents/upload",
        label: "Upload",
        icon: <UploadOutlined />,
        roles: ["Editor", "Admin"],
      },
    ],
  },
  {
    key: "tutors",
    label: "Tutors",
    items: [
      {
        key: "/tutors",
        label: "Tutors",
        icon: <TeamOutlined />,
        roles: ["Auditor", "Editor", "Admin"],
      },
      { key: "/benchmarking", label: "Benchmarking", icon: <ExperimentOutlined />, roles: ["Auditor", "Editor", "Admin"] },
      { key: "/tutor-setup", label: "Tutor Setup", icon: <FileTextOutlined />, roles: ["Editor", "Admin"] },
    ],
  },
  {
    key: "administration",
    label: "Admin",
    items: [
      {
        key: "/admin/roles",
        label: "Role Management",
        icon: <SafetyCertificateOutlined />,
        roles: ["Admin"],
      },
      {
        key: "/admin/embedding-models",
        label: "Embedding Models",
        icon: <DatabaseOutlined />,
        roles: ["Admin"],
      },
      {
        key: "/admin/ai-models",
        label: "AI Models",
        icon: <ApiOutlined />,
        roles: ["Admin"],
      },
      {
        key: "/admin/ai-providers",
        label: "AI Providers",
        icon: <DeploymentUnitOutlined />,
        roles: ["Admin"],
      },
    ],
  },
];

const pageMetadata = [
  { match: (path) => path.startsWith("/tutors/"), title: "Tutor workspace", key: "/tutors" },
  { match: (path) => path === "/", title: "Dashboard", key: "/" },
  { match: (path) => path === "/chat", title: "Chat", key: "/chat" },
  { match: (path) => path === "/tutors", title: "Tutors", key: "/tutors" },
  { match: (path) => path === "/documents/upload", title: "Upload document", key: "/documents/upload" },
  { match: (path) => path.startsWith("/documents"), title: "Documents", key: "/documents" },
  { match: (path) => path === "/benchmarking", title: "Benchmarking", key: "/benchmarking" },
  { match: (path) => path === "/tutor-setup", title: "Tutor setup", key: "/tutor-setup" },
  { match: (path) => path === "/admin/roles", title: "Role Management", key: "/admin/roles" },
  { match: (path) => path === "/admin/embedding-models", title: "Embedding Models", key: "/admin/embedding-models" },
  { match: (path) => path === "/admin/ai-models", title: "AI Models", key: "/admin/ai-models" },
  { match: (path) => path === "/admin/ai-providers", title: "AI Providers", key: "/admin/ai-providers" },
  { match: (path) => path === "/profile", title: "Profile", key: "/profile" },
  { match: (path) => path === "/settings", title: "Settings", key: "/settings" },
];

const canSeeItem = (item, roles) =>
  !item.roles || item.roles.some((role) => roles.includes(role));

export function buildNavigationItems(roles = []) {
  const groups = navigationGroups
    .map((group) => ({
      key: `group-${group.key}`,
      type: "group",
      label: group.label,
      children: group.items.filter((item) => canSeeItem(item, roles)),
    }))
    .filter((group) => group.children.length > 0);

  return [...standaloneNavigationItems, ...groups];
}

export function getPageMetadata(pathname) {
  return pageMetadata.find((page) => page.match(pathname)) ?? {
    title: "Tutor Studio",
    key: "",
  };
}
