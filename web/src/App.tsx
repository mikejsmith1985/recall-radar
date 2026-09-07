// The application shell: loads health and vehicles once, keeps the selected vehicle, and switches
// between the search, ask and evaluation views.
import { useCallback, useEffect, useState } from "react";
import type { ApiClient, HealthReport, Load, Vehicle } from "./api/client";
import { AddVehicleForm } from "./components/AddVehicleForm";
import { LoadStatus } from "./components/LoadStatus";
import { RecentLoads } from "./components/RecentLoads";
import { VehiclePicker } from "./components/VehiclePicker";
import { AskPage } from "./pages/AskPage";
import { EvalPage } from "./pages/EvalPage";
import { SearchPage } from "./pages/SearchPage";

interface AppProps {
  client: ApiClient;
}

export type View = "search" | "ask" | "eval";

const Views: { view: View; label: string }[] = [
  { view: "search", label: "Search" },
  { view: "ask", label: "Ask" },
  { view: "eval", label: "Evaluation" },
];

/** Without a health report the safe assumption is that neither optional service is available. */
export const OfflineHealth: HealthReport = { status: "unknown", database: "unknown", embeddings: "unavailable", answering: "unavailable" };

/**
 * What to say when the server has a database it cannot use. Health reported this field long before
 * anything showed it, so a database that was behind first announced itself as "Internal Server
 * Error" on the add-vehicle form. Only states the server actually names appear here: "unknown"
 * means health has not answered yet, which is not worth alarming anybody about.
 */
export const DatabaseWarnings: Record<string, string> = {
  unavailable: "The server cannot reach its database. Nothing will load until it can.",
  "schema-outdated": "The database is behind this build of the server. Adding or loading a vehicle will fail until its migrations are applied.",
};

export function App({ client }: AppProps) {
  const [view, setView] = useState<View>("search");
  const [health, setHealth] = useState<HealthReport>(OfflineHealth);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [selectedVehicleId, setSelectedVehicleId] = useState<number | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeLoad, setActiveLoad] = useState<Load | null>(null);
  const [vehiclesVersion, setVehiclesVersion] = useState(0);

  useEffect(() => {
    let isCurrent = true;
    client.getHealth().then((loaded) => isCurrent && setHealth(loaded)).catch(() => isCurrent && setHealth(OfflineHealth));
    client
      .listVehicles()
      .then((loaded) => {
        if (!isCurrent) {
          return;
        }
        setVehicles(loaded);
        if (loaded.length > 0) {
          setSelectedVehicleId(loaded[0].id);
        }
      })
      .catch((error: unknown) => isCurrent && setLoadError(error instanceof Error ? error.message : "Vehicles could not be loaded."));
    return () => {
      isCurrent = false;
    };
  }, [client, vehiclesVersion]);

  // A finished load means the vehicle list is stale: it either gained a vehicle or gained its
  // records, and the counts beside each name are now wrong.
  const handleLoadFinished = useCallback((finished: Load) => {
    setActiveLoad(finished);
    setVehiclesVersion((version) => version + 1);
  }, []);

  const databaseWarning = DatabaseWarnings[health.database];
  const isEmbeddingsAvailable = health.embeddings === "ok";
  const isAnsweringAvailable = health.answering === "ok";

  return (
    <div className="app-shell">
      <header className="app-header">
        <h1>Recall Radar</h1>
        <nav className="tab-bar" role="tablist" aria-label="Views">
          {Views.map((entry) => (
            <button key={entry.view} type="button" role="tab" aria-selected={view === entry.view} data-testid={`tab-${entry.view}`} onClick={() => setView(entry.view)}>
              {entry.label}
            </button>
          ))}
        </nav>
      </header>
      <section className="panel">
        {databaseWarning && <p className="error-state" data-testid="database-warning">{databaseWarning}</p>}
        {loadError ? <p className="error-state" data-testid="vehicle-load-error">{loadError}</p> : <VehiclePicker vehicles={vehicles} selectedVehicleId={selectedVehicleId} onSelect={setSelectedVehicleId} />}
        <AddVehicleForm client={client} onLoadStarted={setActiveLoad} />
        {activeLoad && <LoadStatus client={client} load={activeLoad} onFinished={handleLoadFinished} />}
        <details className="recent-loads-panel">
          <summary data-testid="recent-loads-toggle">Recent loads</summary>
          <RecentLoads client={client} refreshToken={vehiclesVersion} />
        </details>
      </section>
      {view === "search" && <SearchPage client={client} vehicleId={selectedVehicleId} isEmbeddingsAvailable={isEmbeddingsAvailable} />}
      {view === "ask" && <AskPage client={client} vehicleId={selectedVehicleId} isEmbeddingsAvailable={isEmbeddingsAvailable} isAnsweringAvailable={isAnsweringAvailable} />}
      {view === "eval" && <EvalPage client={client} />}
    </div>
  );
}
