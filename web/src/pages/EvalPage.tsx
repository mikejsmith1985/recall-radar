// The evaluation page: the latest measured retrieval quality, and how many runs came before it.
import { useEffect, useState } from "react";
import type { ApiClient, EvalReport } from "../api/client";
import { EvalTable } from "../components/EvalTable";

interface EvalPageProps {
  client: ApiClient;
}

export const NoRunsMessage = "No evaluation has run yet. Run the eval CLI verb to measure retrieval quality.";

export function EvalPage({ client }: EvalPageProps) {
  const [report, setReport] = useState<EvalReport | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let isCurrent = true;
    client
      .getEval()
      .then((loaded) => {
        if (isCurrent) {
          setReport(loaded);
        }
      })
      .catch((error: unknown) => {
        if (isCurrent) {
          setErrorMessage(error instanceof Error ? error.message : "The evaluation could not be loaded.");
        }
      });
    return () => {
      isCurrent = false;
    };
  }, [client]);

  if (errorMessage) {
    return <p className="error-state" data-testid="eval-error">{errorMessage}</p>;
  }
  if (report === null) {
    return <p className="empty-state" data-testid="eval-loading">Loading evaluation…</p>;
  }
  if (report.latest === null) {
    return <p className="empty-state" data-testid="eval-empty">{NoRunsMessage}</p>;
  }

  return (
    <section className="panel" data-testid="eval-page">
      <h2>Retrieval quality</h2>
      <p className="meta">
        Ground truth is derived from NHTSA investigations and the recall campaigns they led to; queries are owner complaints
        filed while each investigation was open.
      </p>
      <EvalTable run={report.latest} />
      <p className="meta" data-testid="eval-history-count">
        {report.history.length} earlier run{report.history.length === 1 ? "" : "s"} recorded.
      </p>
    </section>
  );
}
