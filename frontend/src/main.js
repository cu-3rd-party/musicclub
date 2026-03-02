import { createApp } from "vue";
import "./styles/foundations/tokens.css";
import "./styles/foundations/base.css";
import "./styles/shared/layout.css";
import "./styles/shared/navigation.css";
import "./styles/shared/surfaces.css";
import "./styles/shared/buttons.css";
import "./styles/shared/forms.css";
import "./styles/shared/overlays.css";
import App from "./App.vue";

const setViewportHeight = () => {
  const tg = window.Telegram?.WebApp;
  const height = tg?.viewportHeight ?? window.innerHeight;
  if (Number.isFinite(height) && height > 0) {
    document.documentElement.style.setProperty(
      "--tg-viewport-height",
      `${height}px`,
    );
  }
};

setViewportHeight();

const tg = window.Telegram?.WebApp;
tg?.onEvent?.("viewportChanged", setViewportHeight);
window.addEventListener("resize", setViewportHeight);

createApp(App).mount("#app");
