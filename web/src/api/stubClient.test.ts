// Checks the test double fails loudly for calls a test did not expect.
import { createStubClient } from "./stubClient";

describe("createStubClient", () => {
  it("rejects a call the test never named, rather than quietly returning nothing", async () => {
    // A silent stub makes a wrong call look like a working one, which is the failure this prevents.
    const client = createStubClient();

    await expect(client.listVehicles()).rejects.toThrow("listVehicles was not expected in this test");
  });

  it("names the method in the failure, so the surprise call is identifiable", async () => {
    const client = createStubClient();

    await expect(client.getEval()).rejects.toThrow("getEval");
  });

  it("uses the override a test provides", async () => {
    const client = createStubClient({ listLoads: () => Promise.resolve([]) });

    await expect(client.listLoads()).resolves.toEqual([]);
  });

  it("leaves every other method refusing when one is overridden", async () => {
    const client = createStubClient({ listLoads: () => Promise.resolve([]) });

    await expect(client.listVehicles()).rejects.toThrow();
  });

  it("covers every method the client declares", () => {
    // If a method is added to ApiClient and forgotten here, TypeScript fails the build; this checks
    // the object is actually populated rather than relying on that alone.
    const client = createStubClient();

    expect(Object.values(client).every((member) => typeof member === "function")).toBe(true);
    expect(Object.keys(client).length).toBeGreaterThan(0);
  });
});
