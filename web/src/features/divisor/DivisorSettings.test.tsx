import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '../../test/server';
import { DivisorSettings } from './DivisorSettings';

const divisorUrl = '/api/settings/divisor';

function serveDivisor(divisor: number) {
  server.use(http.get(divisorUrl, () => HttpResponse.json({ divisor })));
}

describe('DivisorSettings', () => {
  it('loads and shows the current divisor in a labelled input', async () => {
    serveDivisor(3);

    render(<DivisorSettings />);

    expect(await screen.findByLabelText('Special-case divisor')).toHaveValue(3);
  });

  it('[AC-025] saves a new divisor and confirms it', async () => {
    serveDivisor(3);
    let sent: unknown;
    server.use(
      http.put(divisorUrl, async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json({ divisor: 5 });
      }),
    );
    const user = userEvent.setup();
    render(<DivisorSettings />);

    const input = await screen.findByLabelText('Special-case divisor');
    await user.clear(input);
    await user.type(input, '5');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('status')).toHaveTextContent('Divisor saved: 5');
    expect(sent).toEqual({ divisor: 5 });
  });

  it('[AC-026] shows the server error when the divisor is rejected', async () => {
    serveDivisor(3);
    server.use(
      http.put(divisorUrl, () =>
        HttpResponse.json(
          { status: 400, title: 'Invalid divisor', detail: 'The divisor must be a whole number of at least 1.' },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    const user = userEvent.setup();
    render(<DivisorSettings />);

    const input = await screen.findByLabelText('Special-case divisor');
    await user.clear(input);
    await user.type(input, '0');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Invalid divisor: The divisor must be a whole number of at least 1.',
    );
  });

  it('warns that every transaction will be random when the divisor is 1', async () => {
    serveDivisor(3);
    const user = userEvent.setup();
    render(<DivisorSettings />);

    expect(screen.queryByText(/every transaction will get random change/i)).not.toBeInTheDocument();

    const input = await screen.findByLabelText('Special-case divisor');
    await user.clear(input);
    await user.type(input, '1');

    expect(screen.getByText(/every transaction will get random change/i)).toBeInTheDocument();
  });

  it('[AC-026] shows a generic message when saving fails without a server response', async () => {
    serveDivisor(3);
    server.use(http.put(divisorUrl, () => HttpResponse.error()));
    const user = userEvent.setup();
    render(<DivisorSettings />);

    await screen.findByLabelText('Special-case divisor');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
  });

  it('[AC-025] ignores a load that finishes after the panel was removed', async () => {
    let finishLoad = () => {};
    server.use(
      http.get(
        divisorUrl,
        () =>
          new Promise<Response>((resolve) => {
            finishLoad = () => {
              resolve(HttpResponse.json({ divisor: 9 }));
            };
          }),
      ),
    );
    const { unmount } = render(<DivisorSettings />);

    unmount();
    finishLoad();
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(screen.queryByLabelText('Special-case divisor')).not.toBeInTheDocument();
  });

  it('shows an error when the current divisor cannot be loaded', async () => {
    server.use(http.get(divisorUrl, () => new HttpResponse(null, { status: 503 })));

    render(<DivisorSettings />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Request failed (503)');
  });
});
