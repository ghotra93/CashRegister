import { render, screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { UploadedFileSummary } from '../../api/types';
import { server } from '../../test/server';
import { UploadedFilesList } from './UploadedFilesList';

const newer: UploadedFileSummary = {
  id: 'aaaaaaaa-0000-4000-8000-000000000002',
  fileName: 'tuesday.txt',
  uploadedAt: '2026-09-22T09:30:00+00:00',
  lineCount: 12,
  errorLineCount: 2,
};

const older: UploadedFileSummary = {
  id: 'bbbbbbbb-0000-4000-8000-000000000001',
  fileName: 'monday.txt',
  uploadedAt: '2026-09-21T09:30:00+00:00',
  lineCount: 3,
  errorLineCount: 0,
};

function serveFiles(files: UploadedFileSummary[]) {
  server.use(http.get('/api/files', () => HttpResponse.json(files)));
}

describe('UploadedFilesList', () => {
  it('[AC-027] loads the uploaded files on mount and lists them in server order (newest first)', async () => {
    serveFiles([newer, older]);

    render(<UploadedFilesList refreshKey={0} />);

    const rows = await screen.findAllByRole('row');
    const bodyRows = rows.slice(1);
    expect(bodyRows).toHaveLength(2);
    expect(bodyRows[0]).toHaveTextContent('tuesday.txt');
    expect(bodyRows[0]).toHaveTextContent('12');
    expect(bodyRows[0]).toHaveTextContent('2');
    expect(bodyRows[1]).toHaveTextContent('monday.txt');
  });

  it('[AC-028] gives each entry a download link to its change output', async () => {
    serveFiles([newer, older]);

    render(<UploadedFilesList refreshKey={0} />);

    const link = await screen.findByRole('link', { name: 'Download change for tuesday.txt' });
    expect(link).toHaveAttribute('href', `/api/files/${newer.id}/output`);
    expect(link).toHaveAttribute('download');
  });

  it('[AC-027] reloads when the refresh key changes, showing a newly uploaded file', async () => {
    serveFiles([older]);
    const { rerender } = render(<UploadedFilesList refreshKey={0} />);
    await screen.findByText('monday.txt');

    serveFiles([newer, older]);
    rerender(<UploadedFilesList refreshKey={1} />);

    expect(await screen.findByText('tuesday.txt')).toBeInTheDocument();
  });

  it('shows an empty state when nothing has been uploaded', async () => {
    serveFiles([]);

    render(<UploadedFilesList refreshKey={0} />);

    expect(await screen.findByText('No files uploaded yet.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows an error when the list cannot be loaded', async () => {
    server.use(http.get('/api/files', () => new HttpResponse(null, { status: 500 })));

    render(<UploadedFilesList refreshKey={0} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Request failed (500)');
  });

  it('labels the table columns', async () => {
    serveFiles([older]);

    render(<UploadedFilesList refreshKey={0} />);

    const table = await screen.findByRole('table', { name: 'Uploaded files' });
    const headers = within(table).getAllByRole('columnheader').map((h) => h.textContent);
    expect(headers).toEqual(['File', 'Uploaded', 'Lines', 'Errors', 'Output']);
  });
});
