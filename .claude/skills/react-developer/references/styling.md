# Styling

Follow the project's existing approach. For a new app, pick one and record it in an ADR.

| Approach | Notes |
|---|---|
| **Tailwind CSS** | Default in `create-next-app`. Keep class lists readable; extract a component (not an `@apply` soup) when a pattern repeats. |
| **CSS Modules** (`*.module.css`) | Built in, locally scoped, zero runtime. Good default when Tailwind is not wanted. |
| **Global CSS** | Import only from the root layout; use for resets, tokens and base typography. |
| CSS-in-JS with a runtime | Avoid in the App Router — runtime libraries need client components and extra configuration. |

## Rules

- Define design tokens (colors, spacing, radii) once as CSS custom properties or Tailwind theme values; do not repeat raw hex values.
- Support `prefers-color-scheme` and `prefers-reduced-motion` where the design allows.
- Meet WCAG AA contrast; do not convey meaning by color alone.
- Keep layout responsive with a mobile-first approach; test at narrow widths.
- Do not add a UI component library without user approval; if the project has one, use it before hand-rolling components.
