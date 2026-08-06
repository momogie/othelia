# Othelia — Arsitektur

Dokumentasi arsitektur Othelia: OpenTelemetry dashboard yang menerima telemetry via OTLP, menyimpan ke SQL Server, dan menyajikan traces/spans lewat API + frontend Nuxt.

---

## 1. Gambaran Umum

```
                         OTLP (HTTP/Protobuf, gzip optional)
  AppAPI / service lain  ───────────────▶  POST /v1/traces|metrics|logs
                                                    │   (:4318)
                                                    ▼
                                          ┌──────────────────────────┐
                                          │  Othelia.Api (satu proses)│
                                          │   ┌────────────────────┐  │
                                          │   │ OTLP receiver       │  │
                                          │   │ → parse → filter    │  │
                                          │   │ → bulk insert       │  │
                                          │   └─────────┬──────────┘  │
                                          │             ▼            │
                                          │     SQL Server           │
                                          │     dbo.Spans            │
                                          │     dbo.Settings         │
                                          │             ▲            │
                                          │   ┌─────────┴──────────┐  │
                                          │   │ Query API :5007    │  │
                                          │   │ /api/traces        │  │
                                          │   │ /api/services      │  │
                                          │   │ /api/dashboard     │  │
                                          │   │ /api/settings      │  │
                                          │   └────────────────────┘  │
                                          └──────────┬───────────────┘
                                                     │ /api/** (nginx)
                                                     ▼
                                          Nuxt 4 dashboard (frontend)
```

Satu proses .NET 10 dengan **dua listener Kestrel** (dikonfigurasi di `Program.cs` via `Tracing:Api` & `Tracing:Receiver`):

| Listener | Fungsi |
|---|---|
| `:5007` | Query API + OpenAPI (`/openapi/v1.json`) |
| `:4318` | OTLP receiver (`POST /v1/traces`, `/v1/metrics`, `/v1/logs`) |

---

## 2. Backend — Komponen

### 2.1 Ingress OTLP (`OtlpEndpoints.cs`)

Minimal API group `/v1` dengan 3 handler. `HandleTracesAsync` alurnya:

1. `ReadRequestAsync<T>` — deteksi content type & dekompresi:
   - `gzip` → GZipStream.
   - `protobuf` (default bila kosong) → `MessageParser.ParseFrom`.
   - `json` → `ParseJson` (strip BOM).
2. `OtlpTraceConverter.ConvertRequest` — dari protobuf OTLP menjadi list `SpanRecord` (resource attributes, tags, events, links diserialisasi ke JSON).
3. `resolver.ResolveAsync` — ambil konfigurasi terkini (hot-reload).
4. Jika `Ingestion.Enabled = false` → drop & balas sukses (kosong).
5. `IngestionFilter.Apply` — terapkan aturan filter (Drop/Keep).
6. `store.InsertSpansAsync` — simpan batch ke SQL Server via **`SqlBulkCopy`** (BatchSize 1000).

Handler metrics/logs menerima & memvalidasi payload tapi **belum dipersist** (Phase 2).

### 2.2 Storage (`Observability/TelemetryStore.cs`, `SchemaInitializer.cs`)

- **`SchemaInitializer`** membuat tabel saat startup (jika belum ada):
  - `dbo.Spans` — kolom: `Id, TraceId, SpanId, ParentSpanId, Name, ServiceName, Kind, StartTimeUtc, EndTimeUtc, DurationUs, StatusCode, StatusMessage, HttpMethod, HttpPath, HttpStatusCode, ServiceVersion, ServiceEnvironment, ResourceJson, AttributesJson, EventsJson, LinksJson`.
  - `dbo.Settings` — `[Key] NVARCHAR(200) PK, [Value] NVARCHAR(MAX)` (penyimpanan konfigurasi hot-reload).
- **`TelemetryStore`** (Dapper + `SqlBulkCopy`):
  - `InsertSpansAsync` — batch insert (bulk), menghindari limit 2100 parameter.
  - `QueryRecentTracesAsync(TraceQuery)` — agregasi trace (root span + duration + error + tag services) dengan **WHERE dinamis parameterized**, `LIKE ... ESCAPE`, `COUNT(*) OVER()` → Total, pagination `OFFSET/FETCH`.
  - `QuerySpansAsync`, `QueryServicesAsync`, `QueryDashboardAggregateAsync`, `QueryThroughputAsync`, `DeleteSpansOlderThanAsync`.
- Bila `Tracing:Storage` tidak terisi → **Noop mode** (`NoopStore.cs`): data diterima tapi tidak disimpan (untuk dev tanpa DB).

### 2.3 Konfigurasi Hot-Reload (`TracingOptions.cs`, `TracingOptionsResolver.cs`, `SettingsStore.cs`)

- **`TracingOptions`** dibagi: `Ingestion` (enabled, endpoint, rules), `Storage` (provider, connection string), `Query` (maxTracesPerRequest, defaultLookbackSeconds, slowThresholdMs, dashboardWindowSeconds, dashboardBuckets), `Retention`, `Api`, `Receiver`.
- **`TracingOptionsResolver`**: nilai default dari `appsettings.json` (`Tracing`), di-override patch JSON yang tersimpan di `dbo.Settings` (key `"Tracing"`). Cache 5 detik. Disediakan `ResolveAsync/SaveAsync/ResetAsync`.
- **`SettingsStore`**: `SqlServerSettingsStore` (MERGE upsert) atau `NoopSettingsStore` (untuk dev tanpa DB → `/api/settings` balas 503).
- **`SettingsController`**:
  - `GET /api/settings` → TracingOptions terkini (JSON).
  - `PUT /api/settings` → simpan patch (`TracingSettingsPatch`), efek dalam ≤ 5 detik.
  - `DELETE /api/settings` → reset ke default appsettings.
- Ini dipakai halaman **Settings** di frontend untuk mengubah ingestion/rules/query/retention tanpa restart.

### 2.4 Query API (Controllers)

| Endpoint | Fungsi |
|---|---|
| `GET /api/traces` | Daftar trace ringkas. Query param: `service`, `traceId`, `status` (ok/error/slow), `name`, `nameNot`, `path`, `pathNot`, `minDurationMs`, `maxDurationMs`, `from`, `to`, `limit`, `offset`, `sort`. Header `X-Total-Count` untuk total. |
| `GET /api/traces/{traceId}/spans` | Semua span sebuah trace (waterfall). |
| `GET /api/services` | Daftar service (nama, versi, environment, lastSeen, totalTraces, errorCount). |
| `GET /api/dashboard` | Ringkasan dashboard (metrics, throughput, service health, alerts). |
| `GET /api/dashboard/throughput?range=` | Seri throughput per bucket. |
| `GET /api/settings` | Konfigurasi terkini (hot-reload). |
| `GET /api/health` | Health check sederhana. |

### 2.5 Service Layer

- **`TracingService`** — `GetServicesAsync`, `GetTracesAsync(TraceQuery)`, `GetSpansAsync`. Men-deserialize JSON attributes/events menjadi DTO (`SpanDto`, `TraceSummaryDto`).
- **`DashboardService`** — agregasi dashboard: metrics (totalRequests, errorRate, p50/p99, throughputRps, servicesUp/Down), throughput per bucket, service health (status ok/slow/error berdasar error rate ≥ 5% atau p99 ≥ slowThresholdMs), alerts (error & slow trace terbaru, max 10).

### 2.6 Retention (`RetentionService.cs`)

`BackgroundService` yang menghapus `dbo.Spans` lebih lama dari `Retention.RetentionDays` tiap `CleanupIntervalHours` (minimum 1 jam). Log jumlah yang dihapus.

### 2.7 Ingress Filter (`IngestionFilter.cs`)

Aturan `IngestionFilterRule` (disimpan via Settings): field `ServiceName|SpanName|HttpPath|StatusCode|Kind`, operator `Equals|NotEquals|Contains|Regex` (timeout 100ms), aksi `Drop|Keep`. Rule pertama yang match menang.

---

## 3. Data Model

### `dbo.Spans`

Satu baris = satu span OTLP. Kolom penting:

| Kolom | Isi |
|---|---|
| `TraceId` / `SpanId` / `ParentSpanId` | Identitas trace & hierarki waterfall |
| `Name`, `Kind`, `StatusMessage` | Nama span, kind (Server/Client/Internal), pesan status |
| `ServiceName`, `ServiceVersion`, `ServiceEnvironment` | Resource service |
| `StartTimeUtc`, `EndTimeUtc`, `DurationUs` | Waktu & durasi (microsecond) |
| `StatusCode` | `Unset` / `Ok` / `Error` |
| `HttpMethod`, `HttpPath`, `HttpStatusCode` | HTTP semantik |
| `ResourceJson`, `AttributesJson` | JSON (dictionary) attribute |
| `EventsJson`, `LinksJson` | JSON array event & link |

### `dbo.Settings`

KV store untuk konfigurasi hot-reload (key `Tracing` = JSON `TracingSettingsPatch`).

---

## 4. Frontend (Nuxt 4)

```
frontend/app/
├── assets/css/main.css        # desain sistem (dark/light theme)
├── components/                # AppHeader, AppSidebar, TraceDetail, TraceListItem, TraceWaterfall, MetricCard, LineChart, SpanAttributes, ToastView, ThemeToggle
├── composables/
│   ├── useDashboard.ts        # dashboard: metrics, throughput, services, alerts (GET /api/dashboard)
│   ├── useTracing.ts          # traces: query server-side + pagination (X-Total-Count), services, detail
│   ├── useSettings.ts         # settings: load/save/reset (GET/PUT/DELETE /api/settings)
│   ├── useToast.ts / useTheme.ts / useAppState.ts
├── layouts/default.vue        # shell sidebar + header
├── pages/
│   ├── index.vue              # dashboard
│   ├── traces/index.vue       # daftar trace + filter (time range, status, service, name/path + not, durasi)
│   ├── traces/[id].vue        # detail trace (waterfall, events, attributes)
│   ├── services/index.vue
│   ├── settings.vue           # form konfigurasi hot-reload + ingestion filter rules
│   ├── metrics/, logs/, collectors/, alerts/
└── utils/                     # otelTypes, traceMapper, traceFormat, settingsTypes, mockData
```

- Data di-fetch **server-side** di backend; frontend hanya menampilkan.
- Dev proxy: `nuxt.config.ts` → `devProxy['/api']` → `http://localhost:5007/api`. Di production, route `/api` di-handle nginx (lihat `docs/setup/deployment.md`).
- Traces page: filter dikirim sebagai query param (`from`, `status`, `service`, `name`/`nameNot`, `path`/`pathNot`, `minDurationMs`, `maxDurationMs`, `offset`), total dibaca dari header `X-Total-Count`, "Load more" memakai `offset`.

---

## 5. Referensi Konfigurasi (`appsettings.json` → `Tracing`)

```json
"Tracing": {
  "Ingestion": { "Enabled": true, "Endpoint": "http://0.0.0.0:4318", "Rules": [] },
  "Storage": { "Provider": "SqlServer", "ConnectionString": "Server=...;Database=othelia;..." },
  "Query": {
    "MaxTracesPerRequest": 100, "DefaultLookbackSeconds": 3600, "SlowThresholdMs": 1000,
    "DashboardWindowSeconds": 86400, "DashboardBuckets": 24
  },
  "Api": { "Host": "localhost", "Port": 5007 },
  "Receiver": { "Host": "0.0.0.0", "Port": 4318 },
  "Retention": { "Enabled": true, "RetentionDays": 7, "CleanupIntervalHours": 24 }
}
```

| Key | Keterangan |
|---|---|
| `Ingestion.Rules` | Daftar aturan filter (juga bisa diubah via UI Settings) |
| `Storage.ConnectionString` | Wajib user SQL dengan hak `CREATE TABLE` |
| `Query.*` | Default query & dashboard |
| `Api.Host/Port`, `Receiver.Host/Port` | Binding Kestrel. `"0.0.0.0"` agar bisa diakses host lain |
| `Retention.*` | Cleanup otomatis |

---

## 6. Deployment

Lihat **[`docs/setup/deployment.md`](../setup/deployment.md)**.

## 7. Integrasi

- **Sumber data**: AppAPI (`upahr-backend-new`) mengirim OTLP ke `http://<host-othelia>:4318` (config `OpenTelemetry.Endpoint`). Receiver othelia harus bind `0.0.0.0`.
- **Resilience**: jika othelia down, AppAPI tetap berjalan (eksporter OTLP batch & non-blocking); hanya telemetry yang drop.
