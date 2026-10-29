import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App as AntApp, ConfigProvider } from "antd";
import { Provider, useSelector } from "react-redux";
import "antd/dist/reset.css";
import "./index.css";
import "./styles/workspace.css";
import App from "./App.jsx";
import store from "./store";
import { getAppTheme } from "./theme/appTheme.js";

function ThemedApp() {
  const { themeMode, tableDensity } = useSelector((state) => state.preferences);

  return (
    <ConfigProvider theme={getAppTheme(themeMode)} componentSize={tableDensity}>
      <AntApp>
        <App />
      </AntApp>
    </ConfigProvider>
  );
}

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <Provider store={store}>
      <ThemedApp />
    </Provider>
  </StrictMode>,
);
