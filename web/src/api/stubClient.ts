// A client whose every method refuses, so a test names only the calls it actually expects.
import type { ApiClient } from "./client";

/**
 * Builds an {@link ApiClient} where nothing is implemented, then applies the overrides a test
 * needs. A method the test did not name rejects loudly rather than returning something empty,
 * because a silent stub makes a wrong call look like a working one.
 *
 * It also means adding a method to the client does not send someone editing seven test files.
 */
export function createStubClient(overrides: Partial<ApiClient> = {}): ApiClient {
  const unsupported = (name: string) => () => Promise.reject(new Error(`${name} was not expected in this test`));
  return {
    getHealth: unsupported("getHealth"),
    listVehicles: unsupported("listVehicles"),
    listNhtsaModels: unsupported("listNhtsaModels"),
    registerVehicle: unsupported("registerVehicle"),
    refreshVehicle: unsupported("refreshVehicle"),
    getLoad: unsupported("getLoad"),
    listLoads: unsupported("listLoads"),
    search: unsupported("search"),
    ask: unsupported("ask"),
    getDocument: unsupported("getDocument"),
    getEval: unsupported("getEval"),
    ...overrides,
  };
}
