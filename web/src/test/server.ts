import { setupServer } from 'msw/node';

/** Shared MSW server. Tests register their own handlers with `server.use(...)`. */
export const server = setupServer();
