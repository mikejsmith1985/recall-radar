// Shows what a load is doing, because it takes minutes and the request that started it is long gone.
import { useEffect, useState } from "react";
import type { ApiClient, Load } from "../api/client";

interface LoadStatusProps {
  client: ApiClient;
  load: Load;
  onFinished: (load: Load) => void;
}

/** How often to ask again while a load is running. Loads take minutes, so this need not be fast. */
export const PollIntervalMilliseconds = 2000;

/** What the state means to somebody watching, rather than the bare word the server sends. */
export function describeState(load: Load): string {
  switch (load.state) {
    case "queued":
      return "Waiting to start";
    case "running":
      return "Fetching from NHTSA…";
    case "succeeded":
      return "Loaded";
    default:
      return "Could not load";
  }
}

/** The counts worth showing, in the order they happen. Absent fields are simply skipped. */
export function summariseReport(load: Load): { label: string; value: number }[] {
  const report = load.report;
  if (!report) {
    return [];
  }
  return [
    { label: "complaints", value: report.complaintsNew },
    { label: "recalls", value: report.recallsNew },
    { label: "investigations", value: report.investigationsNew },
    { label: "passages", value: report.chunksCreated },
    { label: "embedded", value: report.chunksEmbedded },
  ].filter((entry): entry is { label: string; value: number } => typeof entry.value === "number");
}

export function LoadStatus({ client, load, onFinished }: LoadStatusProps) {
  const [current, setCurrent] = useState<Load>(load);

  useEffect(() => {
    setCurrent(load);
  }, [load]);

  useEffect(() => {
    if (current.isFinished) {
      return;
    }

    let isCurrent = true;
    const timer = setInterval(() => {
      client
        .getLoad(current.id)
        .then((next) => {
          if (!isCurrent) {
            return;
          }
          setCurrent(next);
          if (next.isFinished) {
            // The vehicle list is stale the moment a load finishes, so the caller refreshes it.
            onFinished(next);
          }
        })
        // A failed poll is not a failed load. Stopping here would strand the view on a stale state.
        .catch(() => undefined);
    }, PollIntervalMilliseconds);

    return () => {
      isCurrent = false;
      clearInterval(timer);
    };
  }, [client, current.id, current.isFinished, onFinished]);

  const counts = summariseReport(current);

  return (
    <div className={`load-status load-${current.state}`} data-testid="load-status" data-state={current.state}>
      <strong data-testid="load-state">{describeState(current)}</strong>{" "}
      <span data-testid="load-vehicle">{current.displayName}</span>
      {counts.length > 0 && (
        <ul data-testid="load-counts">
          {counts.map((entry) => (
            <li key={entry.label}>
              {entry.value} {entry.label}
            </li>
          ))}
        </ul>
      )}
      {current.message && (
        <p className="error-state" data-testid="load-message">
          {current.message}
        </p>
      )}
    </div>
  );
}
