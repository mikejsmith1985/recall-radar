// A single text box with a submit button, shared by the search and ask pages.
import { useState, type FormEvent } from "react";

interface SearchBoxProps {
  label: string;
  placeholder: string;
  submitLabel: string;
  isDisabled: boolean;
  onSubmit: (text: string) => void;
}

/** The API rejects queries shorter than two characters, so the box does too. */
export const MinimumQueryLength = 2;

export function SearchBox({ label, placeholder, submitLabel, isDisabled, onSubmit }: SearchBoxProps) {
  const [text, setText] = useState("");
  const trimmed = text.trim();
  const canSubmit = !isDisabled && trimmed.length >= MinimumQueryLength;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canSubmit) {
      return;
    }
    onSubmit(trimmed);
  }

  return (
    <form className="search-box" onSubmit={handleSubmit} data-testid="search-box">
      <input
        type="text"
        aria-label={label}
        placeholder={placeholder}
        value={text}
        disabled={isDisabled}
        onChange={(event) => setText(event.target.value)}
      />
      <button type="submit" disabled={!canSubmit}>
        {submitLabel}
      </button>
    </form>
  );
}
