// Smoke test: the entry point mounts the shell into a root element using the browser's fetch.
import { act } from "@testing-library/react";

describe("main", () => {
  it("mounts Recall Radar into the root element", async () => {
    // Imported before any #root exists so the module's own auto-mount stays idle and this test owns the container.
    const { mountRecallRadar, RootElementId } = await import("./main");
    const container = document.createElement("div");
    container.id = RootElementId;
    document.body.appendChild(container);
    const originalFetch = window.fetch;
    window.fetch = () => Promise.resolve(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }));

    try {
      let root: ReturnType<typeof mountRecallRadar> | undefined;
      await act(async () => {
        root = mountRecallRadar(container);
      });

      expect(container.querySelector("h1")?.textContent).toBe("Recall Radar");
      await act(async () => root?.unmount());
    } finally {
      window.fetch = originalFetch;
      container.remove();
    }
  });
});
