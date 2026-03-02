import ReactDOM from "react-dom/client";
import "./styles/global.css";
import App from "./components/App";
import ErrorBoundary from "./components/ErrorBoundary";

const setViewportHeight = () => {
  const tg = window.Telegram?.WebApp;
  const height = tg?.viewportHeight ?? window.innerHeight;
  if (Number.isFinite(height) && height > 0) {
    document.documentElement.style.setProperty("--tg-viewport-height", `${height}px`);
  }
};

setViewportHeight();

const tg = window.Telegram?.WebApp;
tg?.onEvent?.("viewportChanged", setViewportHeight);
window.addEventListener("resize", setViewportHeight);

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <ErrorBoundary>
    <App />
  </ErrorBoundary>
);
