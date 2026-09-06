// Renders one evaluation run: recall@5, recall@10 and MRR per retrieval mode in each pool.
import { metricsKey } from "../api/client";
import type { EvalRun, ModeMetrics, RetrievalMode, RetrievalScope } from "../api/client";

interface EvalTableProps {
  run: EvalRun;
}

const MetricDecimals = 3;
const PercentDecimals = 1;

const ModeRows: { mode: RetrievalMode; label: string }[] = [
  { mode: "dense", label: "Meaning (dense)" },
  { mode: "sparse", label: "Keyword (sparse)" },
  { mode: "hybrid", label: "Combined (hybrid)" },
];

const PoolSections: { scope: RetrievalScope; heading: string; caption: string }[] = [
  {
    scope: "all",
    heading: "Every record",
    caption:
      "All complaints, recalls and investigations ranked together. Complaints outnumber the rest by " +
      "orders of magnitude, so they take most places.",
  },
  {
    scope: "campaigns",
    heading: "Recalls and investigations only",
    caption:
      "The same questions, ranked against the official records alone. The gap between the two " +
      "sections is how much of the failure was crowding rather than ranking.",
  },
];

export function formatMetric(value: number): string {
  return value.toFixed(MetricDecimals);
}

/** Faithfulness as "verified / emitted (percent)", or a dash when no citations were emitted. */
export function formatFaithfulness(emitted: number, verified: number): string {
  if (emitted === 0) {
    return "no citations emitted";
  }
  const percent = ((verified / emitted) * 100).toFixed(PercentDecimals);
  return `${verified} / ${emitted} (${percent}%)`;
}

function renderModeRow(
  label: string,
  mode: RetrievalMode,
  scope: RetrievalScope,
  metrics: ModeMetrics | null | undefined,
) {
  const testId = scope === "all" ? `eval-row-${mode}` : `eval-row-campaigns-${mode}`;
  if (!metrics) {
    return (
      <tr key={testId} data-testid={testId}>
        <th scope="row">{label}</th>
        <td colSpan={3} className="empty-state">
          skipped (embeddings unavailable)
        </td>
      </tr>
    );
  }
  return (
    <tr key={testId} data-testid={testId}>
      <th scope="row">{label}</th>
      <td className="number">{formatMetric(metrics.recallAt5)}</td>
      <td className="number">{formatMetric(metrics.recallAt10)}</td>
      <td className="number">{formatMetric(metrics.mrr)}</td>
    </tr>
  );
}

function renderPool(run: EvalRun, scope: RetrievalScope, heading: string, caption: string) {
  return (
    <div key={scope} data-testid={`eval-pool-${scope}`}>
      <h4>{heading}</h4>
      <p className="caption">{caption}</p>
      <table className="eval-table">
        <thead>
          <tr>
            <th scope="col">Mode</th>
            <th scope="col">Recall@5</th>
            <th scope="col">Recall@10</th>
            <th scope="col">MRR</th>
          </tr>
        </thead>
        <tbody>
          {ModeRows.map((row) =>
            renderModeRow(row.label, row.mode, scope, run.metrics[metricsKey(row.mode, scope)] as ModeMetrics | null),
          )}
        </tbody>
      </table>
    </div>
  );
}

export function EvalTable({ run }: EvalTableProps) {
  const faithfulness = run.metrics.faithfulness;
  return (
    <div data-testid="eval-table">
      <p className="meta">
        Run #{run.id} · {run.ranAt} · {run.caseCount} ground-truth cases
      </p>
      {PoolSections.map((section) => renderPool(run, section.scope, section.heading, section.caption))}
      <p data-testid="faithfulness">
        Citation faithfulness:{" "}
        {faithfulness ? formatFaithfulness(faithfulness.emitted, faithfulness.verified) : "not measured (answering unavailable)"}
      </p>
    </div>
  );
}
