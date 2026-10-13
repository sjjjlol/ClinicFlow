import { createRoot } from "react-dom/client";
import OisApp from "./OisApp";
import LegacyApp from "./LegacyApp";
import "./style.css";
createRoot(document.getElementById("root")!).render(
  location.pathname.startsWith("/legacy") ? <LegacyApp /> : <OisApp />,
);
