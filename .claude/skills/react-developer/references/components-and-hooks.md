# Components and hooks

## Components

- Function components only. Type props with an `interface` or `type`; do not use `React.FC`.
- Export one component per file named after the file (`OrderTable.tsx` → `OrderTable`).
- Prefer composition (`children`, slot props) over boolean-flag props that multiply variants.
- Derive values during render instead of mirroring props into state.
- Lists need a stable, unique `key` from the data — never the array index for data that can be reordered, inserted into, or filtered.
- Keep components small enough that their tests read as a short story; extract a child when a component has more than one reason to change.

```tsx
interface OrderRowProps {
  order: Order;
  onCancel: (orderId: string) => void;
}

export function OrderRow({ order, onCancel }: OrderRowProps) {
  return (
    <tr>
      <td>{order.reference}</td>
      <td><button type="button" onClick={() => onCancel(order.id)}>Cancel</button></td>
    </tr>
  );
}
```

## Hook rules

- Call hooks at the top level of a component or custom hook — never in conditions, loops, or after an early return.
- Custom hooks start with `use` and encapsulate one concern.
- Keep the `react-hooks` ESLint rules on (`rules-of-hooks`, `exhaustive-deps`); never silence `exhaustive-deps` without a comment explaining why.

## Effects — and when not to use them

`useEffect` synchronizes with something **outside React** (a subscription, a non-React widget, the document title). It is not for:

| Instead of an effect for… | Do this |
|---|---|
| Fetching data a Server Component could read | Read it on the server |
| Fetching client-side server state | TanStack Query |
| Computing a value from props/state | Compute during render (`useMemo` only if measured as expensive) |
| Resetting state when a prop changes | Give the component a `key` |
| Reacting to a user event | Do it in the event handler |

Every effect that subscribes must return a cleanup.

## State

- `useState` for local state, `useReducer` when transitions have names or several fields change together.
- Lift state only as far as the nearest common owner. Use context for low-frequency shared values (theme, current user), not for fast-changing data.
- URL-worthy state (filters, pagination, selected tab) belongs in `searchParams`, so it survives reload and can be linked.

## Refs

- In React 19, `ref` is a regular prop for function components; `forwardRef` is no longer required.
- Use refs for DOM access and values that must not trigger renders, never to bypass state.
