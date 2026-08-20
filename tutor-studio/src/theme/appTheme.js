import { theme as antdTheme } from "antd";

const sharedToken = {
  colorPrimary: "#4F46E5",
  colorInfo: "#4F46E5",
  colorSuccess: "#12B76A",
  colorWarning: "#F79009",
  colorError: "#D92D20",
  borderRadius: 8,
  borderRadiusLG: 10,
  controlHeight: 40,
  fontFamily:
    "Inter, ui-sans-serif, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
  fontSize: 14,
  fontSizeHeading1: 28,
  fontSizeHeading2: 22,
  fontSizeHeading3: 18,
  lineHeight: 1.5,
};

export const lightTheme = {
  cssVar: { prefix: "bitcraft" },
  token: {
    ...sharedToken,
    colorBgBase: "#FFFFFF",
    colorBgLayout: "#F5F7FA",
    colorBgContainer: "#FFFFFF",
    colorText: "#101828",
    colorTextSecondary: "#667085",
    colorBorder: "#DDE2E9",
    colorBorderSecondary: "#E9ECF1",
  },
  components: {
    Layout: {
      bodyBg: "#F5F7FA",
      headerBg: "#FFFFFF",
      siderBg: "#FFFFFF",
    },
    Menu: {
      itemBg: "transparent",
      itemColor: "#475467",
      itemHoverBg: "#F2F4F7",
      itemHoverColor: "#344054",
      itemSelectedBg: "#EEF2FF",
      itemSelectedColor: "#4338CA",
      itemBorderRadius: 7,
      itemHeight: 40,
      itemMarginInline: 12,
      groupTitleColor: "#98A2B3",
    },
    Button: {
      primaryShadow: "none",
      dangerShadow: "none",
      defaultShadow: "none",
    },
    Card: {
      boxShadowTertiary: "none",
    },
    Table: {
      headerBg: "#F8FAFC",
      headerColor: "#475467",
      borderColor: "#E9ECF1",
      rowHoverBg: "#F8FAFC",
    },
  },
};

export const darkTheme = {
  cssVar: { prefix: "bitcraft" },
  algorithm: antdTheme.darkAlgorithm,
  token: {
    ...sharedToken,
    colorBgBase: "#0B0F19",
    colorBgLayout: "#0B0F19",
    colorBgContainer: "#141A2A",
    colorText: "#E4E7EC",
    colorTextSecondary: "#98A2B3",
    colorBorder: "#2A3246",
    colorBorderSecondary: "#1F2537",
  },
  components: {
    Layout: {
      bodyBg: "#0B0F19",
      headerBg: "#141A2A",
      siderBg: "#141A2A",
    },
    Menu: {
      itemBg: "transparent",
      itemColor: "#98A2B3",
      itemHoverBg: "#1C2333",
      itemHoverColor: "#E4E7EC",
      itemSelectedBg: "#2A2F6D",
      itemSelectedColor: "#A5B4FC",
      itemBorderRadius: 7,
      itemHeight: 40,
      itemMarginInline: 12,
      groupTitleColor: "#667085",
    },
    Button: {
      primaryShadow: "none",
      dangerShadow: "none",
      defaultShadow: "none",
    },
    Card: {
      boxShadowTertiary: "none",
    },
    Table: {
      headerBg: "#141A2A",
      headerColor: "#98A2B3",
      borderColor: "#1F2537",
      rowHoverBg: "#1C2333",
    },
  },
};

export function getAppTheme(mode) {
  return mode === "dark" ? darkTheme : lightTheme;
}

// Retained for backward compatibility with any direct import.
export const appTheme = lightTheme;
