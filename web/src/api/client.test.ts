import { http, HttpResponse } from 'msw';
import { server } from '../test/server';
import { ApiError, getDivisor, listFiles, outputUrl, setDivisor, uploadFile } from './client';
import type { UploadedFileSummary } from './types';

const summary: UploadedFileSummary = {
  id: '0f8fad5b-d9cb-469f-a165-70867728950e',
  fileName: 'input.txt',
  uploadedAt: '2026-09-21T12:00:00+00:00',
  lineCount: 3,
  errorLineCount: 0,
};

function problem(status: number, title: string, detail: string) {
  return HttpResponse.json({ status, title, detail }, { status, headers: { 'Content-Type': 'application/problem+json' } });
}

describe('api client', () => {
  it('[AC-015] turns a ProblemDetails response into an ApiError with its title and detail', async () => {
    server.use(http.post('/api/files', () => problem(400, 'Too many lines', 'A file may contain at most 1000 non-blank lines.')));

    const error = await uploadFile(new File(['x'], 'big.txt')).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 400, title: 'Too many lines', detail: 'A file may contain at most 1000 non-blank lines.' });
    expect((error as ApiError).message).toBe('Too many lines: A file may contain at most 1000 non-blank lines.');
  });

  it('[AC-015] falls back to the HTTP status when the error body is not ProblemDetails', async () => {
    server.use(http.get('/api/files', () => new HttpResponse('gateway down', { status: 502 })));

    await expect(listFiles()).rejects.toMatchObject({ status: 502, title: 'Request failed (502)' });
  });

  it('[AC-015] uses the HTTP status as the title when a JSON error has no title', async () => {
    server.use(http.get('/api/files', () => HttpResponse.json({ detail: 'no title here' }, { status: 400 })));

    await expect(listFiles()).rejects.toMatchObject({ status: 400, title: 'Request failed (400)', detail: 'no title here' });
  });

  it('[AC-015] falls back to the HTTP status when the error has no content type', async () => {
    server.use(http.get('/api/files', () => new HttpResponse(null, { status: 503 })));

    await expect(listFiles()).rejects.toMatchObject({ status: 503, title: 'Request failed (503)' });
  });

  it('lists uploaded files as typed summaries', async () => {
    server.use(http.get('/api/files', () => HttpResponse.json([summary])));

    await expect(listFiles()).resolves.toEqual([summary]);
  });

  it('uploads the file as multipart form data in a field named "file"', async () => {
    let receivedName: string | undefined;
    server.use(
      http.post('/api/files', async ({ request }) => {
        const field = (await request.formData()).get('file');
        receivedName = field instanceof File ? field.name : undefined;
        return HttpResponse.json(summary, { status: 201 });
      }),
    );

    await expect(uploadFile(new File(['2.12,3.00'], 'input.txt'))).resolves.toEqual(summary);
    expect(receivedName).toBe('input.txt');
  });

  it('builds the download URL for a file id', () => {
    expect(outputUrl(summary.id)).toBe(`/api/files/${summary.id}/output`);
  });

  it('reads the divisor', async () => {
    server.use(http.get('/api/settings/divisor', () => HttpResponse.json({ divisor: 3 })));

    await expect(getDivisor()).resolves.toEqual({ divisor: 3 });
  });

  it('sends the new divisor as JSON with PUT', async () => {
    let body: unknown;
    server.use(
      http.put('/api/settings/divisor', async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ divisor: 5 });
      }),
    );

    await expect(setDivisor(5)).resolves.toEqual({ divisor: 5 });
    expect(body).toEqual({ divisor: 5 });
  });
});
