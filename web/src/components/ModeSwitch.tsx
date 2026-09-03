// Chooses between meaning-based, keyword-based and combined ranking. Modes that need embeddings
// are disabled, with the reason shown, whenever the API reports embeddings are unavailable.
import type { RetrievalMode } from "../api/client";

interface ModeSwitchProps {
  mode: RetrievalMode;
  isEmbeddingsAvailable: boolean;
  onChange: (mode: RetrievalMode) => void;
}

interface ModeOption {
  mode: RetrievalMode;
  label: string;
  needsEmbeddings: boolean;
}

export const ModeOptions: ModeOption[] = [
  { mode: "hybrid", label: "Combined", needsEmbeddings: true },
  { mode: "dense", label: "Meaning", needsEmbeddings: true },
  { mode: "sparse", label: "Keyword", needsEmbeddings: false },
];

export const EmbeddingsUnavailableExplanation =
  "Meaning-based ranking is off: no embedding key is configured, so only keyword ranking is available.";

/** Picks the strongest mode the API can currently serve. */
export function chooseDefaultMode(isEmbeddingsAvailable: boolean): RetrievalMode {
  return isEmbeddingsAvailable ? "hybrid" : "sparse";
}

export function ModeSwitch({ mode, isEmbeddingsAvailable, onChange }: ModeSwitchProps) {
  return (
    <div className="mode-switch" role="radiogroup" aria-label="Ranking mode" data-testid="mode-switch">
      {ModeOptions.map((option) => {
        const isDisabled = option.needsEmbeddings && !isEmbeddingsAvailable;
        return (
          <button
            key={option.mode}
            type="button"
            role="radio"
            aria-checked={mode === option.mode}
            disabled={isDisabled}
            data-testid={`mode-${option.mode}`}
            onClick={() => onChange(option.mode)}
          >
            {option.label}
          </button>
        );
      })}
      {!isEmbeddingsAvailable && (
        <span className="explanation" data-testid="mode-explanation">
          {EmbeddingsUnavailableExplanation}
        </span>
      )}
    </div>
  );
}
