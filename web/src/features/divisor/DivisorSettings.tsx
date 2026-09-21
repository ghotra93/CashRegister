import { useEffect, useId, useState, type SubmitEvent } from 'react';
import { ApiError, getDivisor, setDivisor } from '../../api/client';

/** A divisor of 1 divides every amount, so every transaction gets random change. */
const everyAmountDivisor = 1;

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Something went wrong. Please try again.';
}

/** Shows and changes the special-case divisor (AC-025). The server validates the value (AC-026). */
export function DivisorSettings() {
  const inputId = useId();
  const [value, setValue] = useState('');
  const [loaded, setLoaded] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savedDivisor, setSavedDivisor] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    getDivisor()
      .then((response) => {
        if (!cancelled) {
          setValue(String(response.divisor));
          setLoaded(true);
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) {
          setError(messageOf(e));
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setSavedDivisor(null);

    try {
      const response = await setDivisor(Number(value));
      setValue(String(response.divisor));
      setSavedDivisor(response.divisor);
    } catch (e: unknown) {
      setError(messageOf(e));
    } finally {
      setSaving(false);
    }
  }

  const everyTransactionRandom = Number(value) === everyAmountDivisor;

  return (
    <section className="card" aria-labelledby={`${inputId}-heading`}>
      <h2 id={`${inputId}-heading`}>Random change rule</h2>
      <p className="subtitle">
        When the amount owed in cents is divisible by this number, the change is given in random denominations.
      </p>

      <form
        onSubmit={(event) => {
          void handleSubmit(event);
        }}
      >
        <label htmlFor={inputId}>Special-case divisor</label>{' '}
        <input
          id={inputId}
          type="number"
          step={1}
          value={value}
          disabled={!loaded}
          onChange={(event) => {
            setValue(event.target.value);
            setSavedDivisor(null);
          }}
        />{' '}
        <button type="submit" disabled={!loaded || saving}>
          Save
        </button>
      </form>

      {everyTransactionRandom && (
        <p className="warning">With a divisor of 1, every transaction will get random change.</p>
      )}
      {savedDivisor !== null && <p role="status">Divisor saved: {savedDivisor}</p>}
      {error !== null && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </section>
  );
}
