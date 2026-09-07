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
  const [hasAnsweredVehicles, setHasAnsweredVehicles] = useState(false);
  const [selectedVehicleId, setSelectedVehicleId] = useState<number | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [activeLoad, setActiveLoad] = useState<Load | null>(null);
  const [vehiclesVersion, setVehiclesVersion] = useState(0);
  const [isAddingVehicle, setIsAddingVehicle] = useState(false);

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
        setHasAnsweredVehicles(true);
        if (loaded.length > 0) {
          setSelectedVehicleId(loaded[0].id);
        }
      })
      .catch((error: unknown) => {
        if (!isCurrent) {
          return;
        }
        setHasAnsweredVehicles(true);
        setLoadError(error instanceof Error ? error.message : "Vehicles could not be loaded.");
      });
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
        <div>
          <h1>Recall Radar</h1>
          {/* Somebody arriving cold has about three seconds to learn what this is. */}
          <p className="tagline">
            Ask what is wrong with your car and get an answer quoted from NHTSA complaints, recalls and
            defect investigations &mdash; every quote checked against the record it came from.
          </p>
        </div>
        <nav className="tab-bar" role="tablist" aria-label="Views">
          {Views.map((entry) => (
            <button key={entry.view} type="button" role="tab" aria-selected={view === entry.view} data-testid={`tab-${entry.view}`} onClick={() => setView(entry.view)}>
              {entry.label}
            </button>
          ))}
        </nav>
      </header>
      <section className="panel">
        {/* The button lives above the list, not below it: the list arrives after the first paint and
            anything under it moves when it does, which is how a click aimed at this button landed on
            nothing. Nothing above the header can shift. */}
        <div className="section-header">
          <h2 className="section-label">Your vehicles</h2>
          {!isAddingVehicle && (
            <button type="button" className="link-button" data-testid="add-vehicle-open" onClick={() => setIsAddingVehicle(true)}>
              Add a vehicle
            </button>
          )}
        </div>
        {databaseWarning && <p className="error-state" data-testid="database-warning">{databaseWarning}</p>}
        {loadError && <p className="error-state" data-testid="vehicle-load-error">{loadError}</p>}
        {/* The list arrives after the first paint. Rendering "no vehicles yet" in the meantime moved
            everything below it the moment the real cards appeared, which is how a click aimed at the
            button underneath landed on nothing. The placeholder holds the same space. */}
        {!loadError && !hasAnsweredVehicles && (
          <div className="vehicle-picker" aria-busy="true" aria-label="Loading vehicles" data-testid="vehicle-picker-loading">
            <div className="vehicle-skeleton" aria-hidden="true" />
          </div>
        )}
        {!loadError && hasAnsweredVehicles && (
          <VehiclePicker vehicles={vehicles} selectedVehicleId={selectedVehicleId} onSelect={setSelectedVehicleId} />
        )}
        <AddVehicleForm
          client={client}
          onLoadStarted={setActiveLoad}
          isOpen={isAddingVehicle}
          onOpenChange={setIsAddingVehicle}
        />
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
