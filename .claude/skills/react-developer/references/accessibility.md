# Accessibility

## Markup

- Use the native element for the job: `<button>` for actions, `<a>`/`<Link>` for navigation, `<form>`, `<label>`, `<fieldset>`/`<legend>`, `<table>` for tabular data. A clickable `<div>` is a defect.
- Every input has a visible `<label htmlFor>`; placeholder text is not a label.
- Every `<img>`/`next/image` has meaningful `alt`, or `alt=""` when decorative.
- One `<h1>` per page and a logical heading order; landmark elements (`<main>`, `<nav>`, `<header>`).
- Set `lang` on `<html>` in the root layout.

## Interaction

- Everything operable with a keyboard, with a visible focus indicator; never `outline: none` without a replacement.
- Dialogs trap focus, close on Escape, and return focus to the trigger. Prefer the native `<dialog>` element or a proven headless library already in the project.
- After client-side navigation or a major async update, move focus or announce the change.

## Dynamic content

- Announce async status and errors with `role="status"` (polite) or `role="alert"` (assertive).
- Validation errors are linked to their field with `aria-describedby` and marked `aria-invalid`.
- Use ARIA only to fill gaps native HTML cannot; wrong ARIA is worse than none.

## Testing

- React Testing Library queries by role and label (`getByRole('button', { name: 'Upload' })`) — a test that cannot find an element by role usually reveals an accessibility bug.
- `eslint-plugin-jsx-a11y` (included in `eslint-config-next`) catches static issues.
- Add `@axe-core/playwright` checks to key e2e flows when the project has it or the user approves it.
