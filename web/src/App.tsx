import { useState } from 'react';
import { DivisorSettings } from './features/divisor/DivisorSettings';
import { FileUpload } from './features/files/FileUpload';
import { UploadedFilesList } from './features/files/UploadedFilesList';

export function App() {
  // Bumped after each upload so the list reloads and shows the new entry (AC-027).
  const [filesVersion, setFilesVersion] = useState(0);

  return (
    <main className="app">
      <header>
        <h1>Cash Register</h1>
        <p className="subtitle">Upload a transaction file to work out the change for each line.</p>
      </header>
      <FileUpload
        onUploaded={() => {
          setFilesVersion((version) => version + 1);
        }}
      />
      <UploadedFilesList refreshKey={filesVersion} />
      <DivisorSettings />
    </main>
  );
}
