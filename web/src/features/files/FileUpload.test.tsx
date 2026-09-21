import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type { UploadedFileSummary } from '../../api/types';
import { server } from '../../test/server';
import { FileUpload } from './FileUpload';

const uploaded: UploadedFileSummary = {
  id: '6f1c2d3e-0000-4000-8000-000000000001',
  fileName: 'input.txt',
  uploadedAt: '2026-09-21T12:00:00+00:00',
  lineCount: 3,
  errorLineCount: 1,
};

function transactionFile(name = 'input.txt') {
  return new File(['2.12,3.00\n1.97,2.00\nbad\n'], name, { type: 'text/plain' });
}

describe('FileUpload', () => {
  it('keeps the upload button disabled until a file is chosen', async () => {
    const user = userEvent.setup();
    render(<FileUpload onUploaded={vi.fn()} />);

    const button = screen.getByRole('button', { name: 'Upload' });
    expect(button).toBeDisabled();

    await user.upload(screen.getByLabelText('Transaction file'), transactionFile());

    expect(button).toBeEnabled();
  });

  it('[AC-027] reports a successful upload so the list can show the new entry', async () => {
    server.use(http.post('/api/files', () => HttpResponse.json(uploaded, { status: 201 })));
    const onUploaded = vi.fn();
    const user = userEvent.setup();
    render(<FileUpload onUploaded={onUploaded} />);

    await user.upload(screen.getByLabelText('Transaction file'), transactionFile());
    await user.click(screen.getByRole('button', { name: 'Upload' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Processed input.txt: 3 lines, 1 with errors');
    expect(onUploaded).toHaveBeenCalledWith(uploaded);
  });

  it('[AC-027] does not send anything when the form is submitted without a file', async () => {
    let requests = 0;
    server.use(
      http.post('/api/files', () => {
        requests++;
        return HttpResponse.json(uploaded, { status: 201 });
      }),
    );
    const onUploaded = vi.fn();
    render(<FileUpload onUploaded={onUploaded} />);

    screen.getByRole('button', { name: 'Upload' }).closest('form')?.requestSubmit();
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(requests).toBe(0);
    expect(onUploaded).not.toHaveBeenCalled();
  });

  it('[AC-027] shows a generic message when the upload fails without a server response', async () => {
    server.use(http.post('/api/files', () => HttpResponse.error()));
    const user = userEvent.setup();
    render(<FileUpload onUploaded={vi.fn()} />);

    await user.upload(screen.getByLabelText('Transaction file'), transactionFile());
    await user.click(screen.getByRole('button', { name: 'Upload' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The upload failed. Please try again.');
  });

  it('[AC-023] shows the server error when the file has too many lines', async () => {
    server.use(
      http.post('/api/files', () =>
        HttpResponse.json(
          { status: 400, title: 'Too many lines', detail: 'A file may contain at most 1000 non-blank lines.' },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const onUploaded = vi.fn();
    const user = userEvent.setup();
    render(<FileUpload onUploaded={onUploaded} />);

    await user.upload(screen.getByLabelText('Transaction file'), transactionFile('big.txt'));
    await user.click(screen.getByRole('button', { name: 'Upload' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Too many lines: A file may contain at most 1000 non-blank lines.',
    );
    expect(onUploaded).not.toHaveBeenCalled();
  });
});
