import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App as AntApp, ConfigProvider } from "antd";
import { Provider } from "react-redux";
import "antd/dist/reset.css";
import "./index.css";
import "./styles/workspace.css";
import App from "./App.jsx";
import store from "./store";
import { appTheme } from "./theme/appTheme.js";

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <Provider store={store}>
      <ConfigProvider theme={appTheme}>
        <AntApp>
          <App />
        </AntApp>
      </ConfigProvider>
    </Provider>
  </StrictMode>,
);
