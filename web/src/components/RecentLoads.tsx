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

/** The moment worth showing: when it ended, else when it began, else when it was asked for. */
export function chooseWhen(load: Load): string {
  return load.finishedAt ?? load.startedAt ?? load.queuedAt;
}

/**
 * The date as a person reads it, in their own timezone.
 *
 * The server speaks ISO 8601 with microseconds and an offset, which is right for a wire format and
 * unreadable in a table. An unparseable value is shown unchanged rather than hidden, because a
 * timestamp nobody can format is still evidence something is wrong upstream.
 */
export function describeWhen(load: Load): string {
  const when = new Date(chooseWhen(load));
  if (Number.isNaN(when.getTime())) {
    return chooseWhen(load);
  }
  return when.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
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
            <td className="when">{describeWhen(load)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
