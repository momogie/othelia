# Othelia

OpenTelemetry dashboard untuk distributed tracing — melihat traces, spans, dan service map secara visual.

## Struktur Project

```
othelia/
├── backend/          # .NET 10 Web API (Controllers)
│   └── src/
│       └── Othelia.Api/
├── frontend/         # Nuxt 4
│   └── app/
│       ├── components/
│       ├── composables/
│       ├── layouts/
│       └── pages/
└── otel-tracing-ui.html   # prototype desain awal (referensi)
```

## Backend (backend/)

- .NET 10 — ASP.NET Core Web API dengan Controllers
- Swagger/OpenAPI di `/swagger`
- CORS diaktifkan untuk dev frontend (`http://localhost:3000`)

```powershell
cd backend
dotnet build
dotnet run --project src/Othelia.Api
```

## Frontend (frontend/)

- Nuxt 4 (SSR)
- Proxy dev `/api` → backend `http://localhost:5000`

```powershell
cd frontend
npm install
npm run dev
```

## Roadmap

- [ ] Scaffolding struktur (ini)
- [ ] Query traces/spans dari backend (OTLP / Tempo / storage)
- [ ] Dashboard, trace list, trace detail (waterfall), service map
- [ ] Migrasi penuh desain prototype ke komponen Nuxt
