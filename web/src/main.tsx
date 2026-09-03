// Browser entry point: mounts the app into #root with a client that talks to the same origin.
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { createApiClient } from "./api/client";
import "./styles.css";

export const RootElementId = "root";

/** Mounts Recall Radar into the given container. Exported so the smoke test can mount into jsdom. */
export function mountRecallRadar(container: HTMLElement) {
  const client = createApiClient((input, init) => window.fetch(input, init));
  const root = createRoot(container);
  root.render(<App client={client} />);
  return root;
}

const rootElement = document.getElementById(RootElementId);
if (rootElement) {
  mountRecallRadar(rootElement);
}
