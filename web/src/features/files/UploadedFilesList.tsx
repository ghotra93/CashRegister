import { useEffect, useId, useState } from 'react';
import { ApiError, listFiles, outputUrl } from '../../api/client';
import type { UploadedFileSummary } from '../../api/types';

interface UploadedFilesListProps {
  /** Change this value to reload the list, e.g. after an upload. */
  refreshKey: number;
}

const uploadedAtFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

/** The uploaded-files list (AC-027), newest first as returned by the server, with a download per entry (AC-028). */
export function UploadedFilesList({ refreshKey }: UploadedFilesListProps) {
  const headingId = useId();
  const [files, setFiles] = useState<UploadedFileSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    listFiles()
      .then((loaded) => {
        if (!cancelled) {
          setFiles(loaded);
          setError(null);
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) {
          setError(e instanceof ApiError ? e.message : 'The file list could not be loaded.');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [refreshKey]);

  return (
    <section className="card" aria-labelledby={headingId}>
      <h2 id={headingId}>Uploaded files</h2>

      {error !== null && (
        <p role="alert" className="error">
          {error}
        </p>
      )}

      {files !== null && files.length === 0 && <p className="subtitle">No files uploaded yet.</p>}

      {files !== null && files.length > 0 && (
        <div className="table-scroll">
          <table aria-labelledby={headingId}>
            <thead>
              <tr>
                <th scope="col">File</th>
                <th scope="col">Uploaded</th>
                <th scope="col">Lines</th>
                <th scope="col">Errors</th>
                <th scope="col">Output</th>
              </tr>
            </thead>
            <tbody>
              {files.map((file) => (
                <tr key={file.id}>
                  <td>{file.fileName}</td>
                  <td>
                    <time dateTime={file.uploadedAt}>{uploadedAtFormat.format(new Date(file.uploadedAt))}</time>
                  </td>
                  <td>{file.lineCount}</td>
                  <td>{file.errorLineCount}</td>
                  <td>
                    <a href={outputUrl(file.id)} download aria-label={`Download change for ${file.fileName}`}>
                      Download
                    </a>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
