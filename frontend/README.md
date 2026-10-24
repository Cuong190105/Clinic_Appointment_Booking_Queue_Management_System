# Clinic Frontend

Production-ready Vue starter for the clinic system: **Vue 3.5 + Vite**, TypeScript (strict), Tailwind
CSS v4, Vue Router with guards, **Pinia**, Axios with interceptors, vue-i18n,
and Vitest + Vue Test Utils.

> Based on `vue-base-starter-main`; app metadata is adjusted for this clinic project.

## Quick start

```bash
npm install
npm run dev        # http://localhost:5173
```

Sign in with **`demo@example.com` / `password`** → open **Users**.

## Scripts

| Script               | Purpose                                            |
| -------------------- | -------------------------------------------------- |
| `npm run dev`        | Vite dev server with HMR                           |
| `npm run build`      | `vue-tsc` type-check + production build to `dist/` |
| `npm run preview`    | Serve the production build locally                 |
| `npm run test:unit`  | Vitest                                             |
| `npm run lint`       | oxlint + ESLint (with `--fix`)                     |
| `npm run format`     | Prettier                                           |
| `npm run type-check` | `vue-tsc` only                                     |

## Architecture

Feature-first modules with **Pinia** stores as the state layer. Each store owns
its slice of state, derived getters, and the async actions that mutate it — the
Users store also holds loading/error status so views stay declarative.

### Folder structure

```
src/
├── config/
│   └── env.ts            # Typed access to import.meta.env (single source)
├── lib/
│   ├── api/
│   │   ├── http.ts       # Axios instance + interceptors + ApiError
│   │   └── token-storage.ts
│   └── i18n/             # vue-i18n init + en/vi locales + setLocale()
├── components/
│   ├── ui/               # BaseButton, BaseInput, BaseCard, BaseSpinner
│   └── layout/
│       └── AppLayout.vue # nav, language switch, logout + <RouterView/>
├── composables/
│   └── useDebouncedRef.ts
├── features/             # Feature-owned api/types/dialogs
│   ├── auth/             # auth.api (mock), auth.types
│   └── users/            # users.api, users.types, UserFormDialog.vue
├── stores/               # Pinia stores (auth, users)
├── views/                # Route targets (Home, Login, Users, NotFound)
├── router/
│   └── index.ts          # Routes (lazy) + global auth guard
├── App.vue               # <RouterView/> shell
└── main.ts               # createApp + Pinia + Router + i18n
```

> Stores live in `stores/` (Pinia convention) while a feature's API, types, and
> feature-specific components live under `features/<name>/`. A store imports
> from its feature; views import the store.

### Path alias

`@/` → `src/` (already configured by the Vue scaffold in `vite.config.ts` and
`tsconfig.app.json`).

### HTTP layer (`lib/api/http.ts`)

- **Request interceptor** attaches the bearer token.
- **Response interceptor** does a **deduplicated** 401 refresh + replay, and on
  failure dispatches an `auth:logout` event (handled in `main.ts` to route to
  `/login`).
- All errors are normalized to **`ApiError`**.

### Auth & route guard

`router/index.ts` registers a global `beforeEach` guard: routes are private
unless `meta.public` is set. Unauthenticated users are sent to `/login` with a
`redirect` query param; authenticated users are bounced away from `/login`.
State lives in `stores/auth.ts`; the mock login is in `features/auth/auth.api.ts`.

### i18n

`lib/i18n/index.ts` creates the `vue-i18n` instance (Composition API mode) and
resolves the initial locale from `localStorage` → browser → default. Use
`setLocale(code)` to switch (the layout's `<select>` calls it). Strings live in
`lib/i18n/locales/{en,vi}.json`.

## How to add a feature

Example: a **Products** feature.

1. `src/features/products/products.types.ts` and `products.api.ts` (use
   `@/lib/api/http`).
2. `src/stores/products.ts` — a Pinia setup store with `items`, `status`, and
   actions (copy `stores/users.ts`).
3. `src/views/ProductsView.vue` — UI using `@/components/ui/*` and
   `storeToRefs(store)`.
4. Add a **lazy** route under the `AppLayout` children in `router/index.ts`.
5. Add a nav `<RouterLink>` in `components/layout/AppLayout.vue` and i18n keys.
6. Add a spec under `__tests__/`.

## Testing

Vitest + Vue Test Utils, jsdom. Example:
`src/components/ui/__tests__/BaseButton.spec.ts`. Run `npm run test:unit`.

## Configuration

Copy `.env.example` → `.env`. Only `VITE_*` vars reach the client; read them via
`src/config/env.ts`.

| Var                     | Meaning                                                           |
| ----------------------- | ----------------------------------------------------------------- |
| `VITE_APP_NAME`         | Display name                                                      |
| `VITE_API_BASE_URL`     | Frontend API path/base URL; use `/api` for local proxy and Docker |
| `VITE_API_PROXY_TARGET` | Vite-only backend target, default `http://localhost:5000`         |
| `VITE_DEFAULT_LOCALE`   | Fallback language                                                 |
| `VITE_ENABLE_MOCK`      | Reserved flag for mock toggles                                    |

## Docker

```bash
docker build -t clinic-frontend .
docker run -p 8080:80 clinic-frontend
```

Multi-stage build (Node → nginx) with SPA fallback + asset caching.
