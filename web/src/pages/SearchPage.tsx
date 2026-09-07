// The explainable-search page: a query, a ranking mode, and results that each say how they were found.
import { useEffect, useState } from "react";
import { ApiError, type ApiClient, type RetrievalMode, type SearchHit, type TrimScope } from "../api/client";
import { chooseDefaultMode, ModeSwitch } from "../components/ModeSwitch";
import { ResultCard } from "../components/ResultCard";
import { TrimSwitch } from "../components/TrimSwitch";
import { SearchBox } from "../components/SearchBox";

interface SearchPageProps {
  client: ApiClient;
  vehicleId: number | null;
  isEmbeddingsAvailable: boolean;
  /** What the selected vehicle's VIN decoded to, or null when no VIN has said which version it is. */
  vehicleTrim: string | null;
}

export const SelectVehiclePrompt = "Pick a vehicle above to search its records.";

/** Shown before the first search: blank space tells nobody what this page is for. */
export const SearchInvitation =
  "Describe a symptom in your own words. Every record that comes back says how it was found: by meaning, by keyword, or by both.";

export function SearchPage({ client, vehicleId, isEmbeddingsAvailable, vehicleTrim }: SearchPageProps) {
  const [mode, setMode] = useState<RetrievalMode>(() => chooseDefaultMode(isEmbeddingsAvailable));
  const [isModePinned, setIsModePinned] = useState(false);
  const [lastQuery, setLastQuery] = useState<string | null>(null);
  const [hits, setHits] = useState<SearchHit[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  // The narrow search is the honest default once a VIN says which version this is: a supercharged
  // V8 shares almost nothing with the V6 filed under the same NHTSA model name.
  const [trims, setTrims] = useState<TrimScope>("thisTrim");
  const [otherTrims, setOtherTrims] = useState<number | null>(null);

  // Health answers after this page has already rendered, so the mode chosen at mount was decided
  // without knowing whether the server can do meaning search at all. Left alone, a server that
  // supports every mode would sit in keyword mode because of an answer that had not arrived yet.
  // A mode somebody picked, or one the server refused, is pinned and never reconsidered.
  useEffect(() => {
    if (!isModePinned) {
      setMode(chooseDefaultMode(isEmbeddingsAvailable));
    }
  }, [isEmbeddingsAvailable, isModePinned]);

  async function runSearch(query: string, requestedMode: RetrievalMode, requestedTrims: TrimScope = trims) {
    if (vehicleId === null) {
      return;
    }
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const response = await client.search({ vehicleId, query, mode: requestedMode, trims: requestedTrims });
      setHits(response.hits);
      setOtherTrims(response.otherTrims);
      setLastQuery(query);
    } catch (error) {
      setHits([]);
      setErrorMessage(error instanceof Error ? error.message : "Search failed.");
      if (error instanceof ApiError && error.isEmbeddingsUnavailable) {
        setMode("sparse");
        setIsModePinned(true);
      }
    } finally {
      setIsLoading(false);
    }
  }

  function handleTrimsChange(nextTrims: TrimScope) {
    setTrims(nextTrims);
    if (lastQuery !== null) {
      void runSearch(lastQuery, mode, nextTrims);
    }
  }

  function handleModeChange(nextMode: RetrievalMode) {
    setMode(nextMode);
    setIsModePinned(true);
    if (lastQuery !== null) {
      void runSearch(lastQuery, nextMode);
    }
  }

  if (vehicleId === null) {
    return <p className="empty-state" data-testid="search-needs-vehicle">{SelectVehiclePrompt}</p>;
  }

  return (
    <section data-testid="search-page">
      <SearchBox
        label="Search phrase"
        placeholder="e.g. steering wheel locks up"
        submitLabel="Search"
        isDisabled={isLoading}
        onSubmit={(query) => void runSearch(query, mode)}
      />
      <ModeSwitch mode={mode} isEmbeddingsAvailable={isEmbeddingsAvailable} onChange={handleModeChange} />
      <TrimSwitch trims={trims} vehicleTrim={vehicleTrim} otherTrims={otherTrims} onChange={handleTrimsChange} />
      {errorMessage && <p className="error-state" data-testid="search-error">{errorMessage}</p>}
      {lastQuery === null && !errorMessage && (
        <p className="empty-state" data-testid="search-invitation">{SearchInvitation}</p>
      )}
      {lastQuery !== null && hits.length === 0 && !errorMessage && (
        <p className="empty-state" data-testid="search-empty">
          No records matched “{lastQuery}”
          {trims === "thisTrim" && vehicleTrim !== null ? ` for a ${vehicleTrim}.` : "."}
          {trims === "thisTrim" && otherTrims !== null && otherTrims > 0 && (
            <>
              {" "}
              <button type="button" className="link-button" data-testid="widen-to-all-trims" onClick={() => handleTrimsChange("allTrims")}>
                Search every version of this model
              </button>
            </>
          )}
        </p>
      )}
      {hits.length > 0 && (
        <ul className="result-list" data-testid="result-list" data-mode={mode}>
          {hits.map((hit, index) => (
            <ResultCard key={hit.chunkId} hit={hit} position={index + 1} />
          ))}
        </ul>
      )}
    </section>
  );
}
