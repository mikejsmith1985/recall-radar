// The explainable-search page: a query, a ranking mode, and results that each say how they were found.
import { useState } from "react";
import { ApiError, type ApiClient, type RetrievalMode, type SearchHit } from "../api/client";
import { chooseDefaultMode, ModeSwitch } from "../components/ModeSwitch";
import { ResultCard } from "../components/ResultCard";
import { SearchBox } from "../components/SearchBox";

interface SearchPageProps {
  client: ApiClient;
  vehicleId: number | null;
  isEmbeddingsAvailable: boolean;
}

export const SelectVehiclePrompt = "Pick a vehicle above to search its records.";

export function SearchPage({ client, vehicleId, isEmbeddingsAvailable }: SearchPageProps) {
  const [mode, setMode] = useState<RetrievalMode>(() => chooseDefaultMode(isEmbeddingsAvailable));
  const [lastQuery, setLastQuery] = useState<string | null>(null);
  const [hits, setHits] = useState<SearchHit[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function runSearch(query: string, requestedMode: RetrievalMode) {
    if (vehicleId === null) {
      return;
    }
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const response = await client.search({ vehicleId, query, mode: requestedMode });
      setHits(response.hits);
      setLastQuery(query);
    } catch (error) {
      setHits([]);
      setErrorMessage(error instanceof Error ? error.message : "Search failed.");
      if (error instanceof ApiError && error.isEmbeddingsUnavailable) {
        setMode("sparse");
      }
    } finally {
      setIsLoading(false);
    }
  }

  function handleModeChange(nextMode: RetrievalMode) {
    setMode(nextMode);
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
      {errorMessage && <p className="error-state" data-testid="search-error">{errorMessage}</p>}
      {lastQuery !== null && hits.length === 0 && !errorMessage && (
        <p className="empty-state" data-testid="search-empty">No records matched “{lastQuery}”.</p>
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
