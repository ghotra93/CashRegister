import { useId, useRef, useState, type SubmitEvent } from 'react';
import { ApiError, uploadFile } from '../../api/client';
import type { UploadedFileSummary } from '../../api/types';

interface FileUploadProps {
  /** Called after the server has processed and stored the file (AC-027). */
  onUploaded: (file: UploadedFileSummary) => void;
}

function summaryText(file: UploadedFileSummary): string {
  return `Processed ${file.fileName}: ${String(file.lineCount)} lines, ${String(file.errorLineCount)} with errors`;
}

/** Uploads a transaction file (one "owed,paid" pair per line) for processing. */
export function FileUpload({ onUploaded }: FileUploadProps) {
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (file === null) {
      return;
    }

    setUploading(true);
    setResult(null);
    setError(null);

    try {
      const uploaded = await uploadFile(file);
      setResult(summaryText(uploaded));
      setFile(null);
      if (inputRef.current !== null) {
        inputRef.current.value = '';
      }
      onUploaded(uploaded);
    } catch (e: unknown) {
      setError(e instanceof ApiError ? e.message : 'The upload failed. Please try again.');
    } finally {
      setUploading(false);
    }
  }

  return (
    <section className="card" aria-labelledby={`${inputId}-heading`}>
      <h2 id={`${inputId}-heading`}>Upload transactions</h2>
      <p className="subtitle">A plain-text file with one &quot;owed,paid&quot; pair per line, e.g. 2.12,3.00 (up to 1000 lines).</p>

      <form
        onSubmit={(event) => {
          void handleSubmit(event);
        }}
      >
        <label htmlFor={inputId}>Transaction file</label>{' '}
        <input
          id={inputId}
          ref={inputRef}
          type="file"
          accept=".txt,.csv,text/plain,text/csv"
          onChange={(event) => {
            setFile(event.target.files?.[0] ?? null);
          }}
        />{' '}
        <button type="submit" disabled={file === null || uploading}>
          Upload
        </button>
      </form>

      {result !== null && <p role="status">{result}</p>}
      {error !== null && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </section>
  );
}
