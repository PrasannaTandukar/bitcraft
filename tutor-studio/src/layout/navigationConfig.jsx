import {
  ApiOutlined,
  DashboardOutlined,
  DatabaseOutlined,
  DeploymentUnitOutlined,
  ExperimentOutlined,
  FileTextOutlined,
  RobotOutlined,
  SafetyCertificateOutlined,
  SettingOutlined,
  TeamOutlined,
} from "@ant-design/icons";

export const navigationGroups = [
  {
    key: "workspace",
    label: "Workspace",
    items: [
      { key: "/", label: "Dashboard", icon: <DashboardOutlined /> },
      { key: "/ai-tutor", label: "AI Tutor", icon: <RobotOutlined /> },
    ],
  },
  {
    key: "knowledge",
    label: "Knowledge",
    items: [
      {
        key: "/tutors",
        label: "Tutors",
        icon: <TeamOutlined />,
        roles: ["Auditor", "Editor", "Admin"],
      },
      {
        key: "/files",
        label: "Files",
        icon: <FileTextOutlined />,
        roles: ["Auditor", "Editor", "Admin"],
      },
    ],
  },
  {
    key: "evaluation",
    label: "Evaluation",
    items: [
      {
        key: "/benchmarking",
        label: "Benchmarking",
        icon: <ExperimentOutlined />,
        roles: ["Auditor", "Editor", "Admin"],
      },
    ],
  },
  {
    key: "administration",
    label: "Administration",
    items: [
      {
        key: "/role-management",
        label: "Role Management",
        icon: <SafetyCertificateOutlined />,
        roles: ["Admin"],
      },
      {
        key: "/model-catalog",
        label: "Model Catalog",
        icon: <DatabaseOutlined />,
        roles: ["Admin"],
      },
      {
        key: "/interaction-model",
        label: "Interaction Models",
        icon: <ApiOutlined />,
        roles: ["Admin"],
      },
      {
        key: "/runtime-engines",
        label: "Runtime Engines",
        icon: <DeploymentUnitOutlined />,
        roles: ["Admin"],
      },
    ],
  },
];

export const utilityNavigationItems = [
  { key: "/settings", label: "Settings", icon: <SettingOutlined /> },
];

const pageMetadata = [
  { match: (path) => path.startsWith("/tutors/"), title: "Tutor workspace", key: "/tutors" },
  { match: (path) => path === "/", title: "Dashboard", key: "/" },
  { match: (path) => path === "/ai-tutor", title: "AI Tutor", key: "/ai-tutor" },
  { match: (path) => path === "/tutors", title: "Tutors", key: "/tutors" },
  { match: (path) => path === "/files", title: "Files", key: "/files" },
  { match: (path) => path === "/benchmarking", title: "Benchmarking", key: "/benchmarking" },
  { match: (path) => path === "/role-management", title: "Role Management", key: "/role-management" },
  { match: (path) => path === "/model-catalog", title: "Model Catalog", key: "/model-catalog" },
  { match: (path) => path === "/interaction-model", title: "Interaction Models", key: "/interaction-model" },
  { match: (path) => path === "/runtime-engines", title: "Runtime Engines", key: "/runtime-engines" },
  { match: (path) => path === "/settings", title: "Settings", key: "/settings" },
];

const canSeeItem = (item, roles) =>
  !item.roles || item.roles.some((role) => roles.includes(role));

export function buildNavigationItems(roles = []) {
  return navigationGroups
    .map((group) => ({
      key: `group-${group.key}`,
      type: "group",
      label: group.label,
      children: group.items.filter((item) => canSeeItem(item, roles)),
    }))
    .filter((group) => group.children.length > 0);
}

export function getPageMetadata(pathname) {
  return pageMetadata.find((page) => page.match(pathname)) ?? {
    title: "Tutor Studio",
    key: "",
  };
}
