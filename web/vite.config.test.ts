// Guards the dev proxy: both API prefixes must reach the .NET API on its documented port.
import viteConfig, { apiDevServerUrl } from "./vite.config";

describe("vite config", () => {
  it("proxies the API and health prefixes to the .NET dev server", () => {
    const resolved = viteConfig as { server?: { proxy?: Record<string, unknown> } };
    const proxy = resolved.server?.proxy ?? {};

    expect(apiDevServerUrl).toBe("http://127.0.0.1:5180");
    expect(proxy["/api"]).toBe(apiDevServerUrl);
    expect(proxy["/health"]).toBe(apiDevServerUrl);
  });

  it("builds into web/dist, which is git-ignored", () => {
    const resolved = viteConfig as { build?: { outDir?: string } };

    expect(resolved.build?.outDir).toBe("dist");
  });
});
