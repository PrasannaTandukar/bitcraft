import { App as AntApp, ConfigProvider } from "antd";
import { useSelector } from "react-redux";
import App from "./App.jsx";
import { getAppTheme } from "./theme/appTheme.js";

export default function ThemedApp() {
  const { themeMode, tableDensity } = useSelector((state) => state.preferences);

  return (
    <ConfigProvider theme={getAppTheme(themeMode)} componentSize={tableDensity}>
      <AntApp>
        <App />
      </AntApp>
    </ConfigProvider>
  );
}

