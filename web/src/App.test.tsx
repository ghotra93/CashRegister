import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type { UploadedFileSummary } from './api/types';
import { server } from './test/server';
import { App } from './App';

const uploaded: UploadedFileSummary = {
  id: 'cccccccc-0000-4000-8000-000000000003',
  fileName: 'readme.txt',
  uploadedAt: '2026-09-21T12:00:00+00:00',
  lineCount: 3,
  errorLineCount: 0,
};

describe('App', () => {
  it('[AC-027] shows a newly uploaded file in the list without a page reload', async () => {
    let files: UploadedFileSummary[] = [];
    server.use(
      http.get('/api/settings/divisor', () => HttpResponse.json({ divisor: 3 })),
      http.get('/api/files', () => HttpResponse.json(files)),
      http.post('/api/files', () => {
        files = [uploaded];
        return HttpResponse.json(uploaded, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    render(<App />);

    expect(await screen.findByText('No files uploaded yet.')).toBeInTheDocument();

    await user.upload(screen.getByLabelText('Transaction file'), new File(['2.12,3.00'], 'readme.txt'));
    await user.click(screen.getByRole('button', { name: 'Upload' }));

    expect(await screen.findByRole('link', { name: 'Download change for readme.txt' })).toBeInTheDocument();
  });
});
