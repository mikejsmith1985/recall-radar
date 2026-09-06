// Shows the last few loads, so a scheduled refresh that failed overnight is visible rather than silent.
import { useEffect, useState } from "react";
import type { ApiClient, Load } from "../api/client";

interface RecentLoadsProps {
  client: ApiClient;
  /** Changes whenever a load finishes, so the list re-reads rather than showing a stale run. */
  refreshToken: number;
}

/** How many to show. Enough to see last night's refresh, not a log. */
export const VisibleLoadCount = 5;

/** The date as a person reads it, or the queued time when a load has not finished. */
export function describeWhen(load: Load): string {
  return load.finishedAt ?? load.startedAt ?? load.queuedAt;
}

export function RecentLoads({ client, refreshToken }: RecentLoadsProps) {
  const [loads, setLoads] = useState<Load[]>([]);
  const [problem, setProblem] = useState<string | null>(null);

  useEffect(() => {
    let isCurrent = true;
    client
      .listLoads()
      .then((loaded) => isCurrent && setLoads(loaded.slice(0, VisibleLoadCount)))
      .catch((error: unknown) => isCurrent && setProblem(error instanceof Error ? error.message : "Loads could not be read."));
    return () => {
      isCurrent = false;
    };
  }, [client, refreshToken]);

  if (problem) {
    return (
      <p className="error-state" data-testid="recent-loads-error">
        {problem}
      </p>
    );
  }

  if (loads.length === 0) {
    return (
      <p className="empty-state" data-testid="recent-loads-empty">
        No records have been loaded through the app yet.
      </p>
    );
  }

  return (
    <table className="recent-loads" data-testid="recent-loads">
      <thead>
        <tr>
          <th scope="col">Vehicle</th>
          <th scope="col">Result</th>
          <th scope="col">Started by</th>
          <th scope="col">When</th>
        </tr>
      </thead>
      <tbody>
        {loads.map((load) => (
          <tr key={load.id} data-testid={`recent-load-${load.id}`} data-state={load.state}>
            <th scope="row">{load.displayName}</th>
            <td>{load.state === "failed" ? <span className="error-state">{load.message ?? "failed"}</span> : load.state}</td>
            <td>{load.trigger}</td>
            <td>{describeWhen(load)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
