// The grounded-answer page: describe a symptom, read the answer, and open any citation to see the
// exact quoted span inside the record the answer rests on.
import { useState } from "react";
import type { ApiClient, AskResponse, Citation, SourceDocument } from "../api/client";
import { AnswerPanel } from "../components/AnswerPanel";
import { CitationView } from "../components/CitationView";
import { KnownPatternPanel } from "../components/KnownPatternPanel";
import { chooseDefaultMode } from "../components/ModeSwitch";
import { SearchBox } from "../components/SearchBox";

interface AskPageProps {
  client: ApiClient;
  vehicleId: number | null;
  isEmbeddingsAvailable: boolean;
  isAnsweringAvailable: boolean;
}

export const SelectVehiclePrompt = "Pick a vehicle above before asking about a symptom.";
export const AnsweringUnavailableMessage =
  "Answering is off: no Anthropic key is configured. Search still works in the Search tab.";

interface OpenedCitation {
  citation: Citation;
  document: SourceDocument;
}

export function AskPage({ client, vehicleId, isEmbeddingsAvailable, isAnsweringAvailable }: AskPageProps) {
  const [response, setResponse] = useState<AskResponse | null>(null);
  const [opened, setOpened] = useState<OpenedCitation | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function ask(question: string) {
    if (vehicleId === null) {
      return;
    }
    setIsLoading(true);
    setErrorMessage(null);
    setOpened(null);
    try {
      setResponse(await client.ask({ vehicleId, question, mode: chooseDefaultMode(isEmbeddingsAvailable) }));
    } catch (error) {
      setResponse(null);
      setErrorMessage(error instanceof Error ? error.message : "The question could not be answered.");
    } finally {
      setIsLoading(false);
    }
  }

  async function openCitation(citation: Citation) {
    try {
      const document = await client.getDocument(citation.documentId);
      setOpened({ citation, document });
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "The record could not be loaded.");
    }
  }

  if (vehicleId === null) {
    return <p className="empty-state" data-testid="ask-needs-vehicle">{SelectVehiclePrompt}</p>;
  }

  return (
    <section data-testid="ask-page">
      {!isAnsweringAvailable && (
        <p className="error-state" data-testid="answering-unavailable">{AnsweringUnavailableMessage}</p>
      )}
      <SearchBox
        label="Symptom"
        placeholder="e.g. exhaust smell inside the cabin"
        submitLabel="Ask"
        isDisabled={isLoading || !isAnsweringAvailable}
        onSubmit={(question) => void ask(question)}
      />
      {errorMessage && <p className="error-state" data-testid="ask-error">{errorMessage}</p>}
      {response && (
        <>
          <AnswerPanel response={response} onOpenCitation={(citation) => void openCitation(citation)} />
          <KnownPatternPanel response={response} />
        </>
      )}
      {opened && <CitationView document={opened.document} citation={opened.citation} />}
    </section>
  );
}
