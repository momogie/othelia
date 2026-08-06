# Othelia

OpenTelemetry dashboard untuk distributed tracing — menerima telemetry via OTLP, menyimpannya ke SQL Server, dan menampilkan traces/spans secara visual.

## Dokumentasi

- [Arsitektur](docs/architecture/README.md) — komponen, alur data, skema DB, referensi konfigurasi.
- [Setup & Deployment](docs/setup/deployment.md) — panduan deploy di Linux (backend, frontend, nginx, SQL Server).

## Struktur Project

```
othelia/
├── backend/          # .NET 10 Web API + OTLP receiver
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

`.NET 10 — ASP.NET Core Web API`. Satu proses dengan **dua listener**:

| Port | Fungsi |
|------|--------|
| `5007` | Query API (`/api/traces`, `/api/traces/{id}/spans`, `/api/services`) + OpenAPI di `/openapi/v1.json` |
| `4318` | OTLP receiver (`POST /v1/traces`, `/v1/metrics`, `/v1/logs`) — protobuf/JSON, mendukung gzip |

Storage: **SQL Server** (database `othelia`, tabel `dbo.Spans`), diakses via Dapper. Skema dibuat otomatis saat startup. Konfigurasi di `appsettings.json` → `Tracing:Storage`.

- Trace OTLP dipersist (spans + attributes + events).
- Metrics & Logs diterima (response valid) tapi belum dipersist — **Phase 2**.

Prerequisite: SQL Server + database `othelia` sudah ada (kredensial di `appsettings.json`).

```powershell
cd backend
dotnet build
dotnet run --project src/Othelia.Api
```

Contoh kirim trace (JSON OTLP):

```powershell
curl -X POST http://localhost:4318/v1/traces `
  -H "Content-Type: application/json" --data-binary "@trace.json"
```

## Frontend (frontend/)

- Nuxt 4 (SSR)
- Proxy dev `/api` → backend `http://localhost:5007/api`
- Traces page + dashboard menampilkan **data nyata** dari query API backend

```powershell
cd frontend
npm install
npm run dev
```

## Roadmap

- [x] Scaffolding struktur
- [x] OTLP receiver (`/v1/traces`, `/v1/metrics`, `/v1/logs`) di `:4318`
- [x] Storage SQL Server (`dbo.Spans`) + schema init otomatis
- [x] Query API traces/spans/services + frontend data nyata
- [ ] Persist metrics & logs (Phase 2)
- [ ] Migrasi penuh desain prototype ke komponen Nuxt (service map, alerts, dst.)
