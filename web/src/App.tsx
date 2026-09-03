// The application shell: loads health and vehicles once, keeps the selected vehicle, and switches
// between the search, ask and evaluation views.
import { useEffect, useState } from "react";
import type { ApiClient, HealthReport, Vehicle } from "./api/client";
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

export function App({ client }: AppProps) {
  const [view, setView] = useState<View>("search");
  const [health, setHealth] = useState<HealthReport>(OfflineHealth);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [selectedVehicleId, setSelectedVehicleId] = useState<number | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

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
  }, [client]);

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
        {loadError ? <p className="error-state" data-testid="vehicle-load-error">{loadError}</p> : <VehiclePicker vehicles={vehicles} selectedVehicleId={selectedVehicleId} onSelect={setSelectedVehicleId} />}
      </section>
      {view === "search" && <SearchPage client={client} vehicleId={selectedVehicleId} isEmbeddingsAvailable={isEmbeddingsAvailable} />}
      {view === "ask" && <AskPage client={client} vehicleId={selectedVehicleId} isEmbeddingsAvailable={isEmbeddingsAvailable} isAnsweringAvailable={isAnsweringAvailable} />}
      {view === "eval" && <EvalPage client={client} />}
    </div>
  );
}
