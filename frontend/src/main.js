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

const applyTelegramTheme = () => {
  const tg = window.Telegram?.WebApp;
  const themeParams = tg?.themeParams;
  if (!themeParams) return;

  const root = document.documentElement;
  const map = {
    "--tg-theme-bg-color": themeParams.bg_color,
    "--tg-theme-secondary-bg-color": themeParams.secondary_bg_color,
    "--tg-theme-text-color": themeParams.text_color,
    "--tg-theme-hint-color": themeParams.hint_color,
    "--tg-theme-link-color": themeParams.link_color,
    "--tg-theme-button-color": themeParams.button_color,
    "--tg-theme-button-text-color": themeParams.button_text_color,
  };

  Object.entries(map).forEach(([key, value]) => {
    if (value) {
      root.style.setProperty(key, value);
    }
  });

  if (tg.colorScheme) {
    root.dataset.tgColorScheme = tg.colorScheme;
  }
};

setViewportHeight();
applyTelegramTheme();

const tg = window.Telegram?.WebApp;
tg?.onEvent?.("viewportChanged", setViewportHeight);
tg?.onEvent?.("themeChanged", applyTelegramTheme);
window.addEventListener("resize", setViewportHeight);

createApp(App).mount("#app");
