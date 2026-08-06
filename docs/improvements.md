# Othelia — Analisis & Rencana Improvement

Dokumen ini berisi analisis mendetail terhadap proyek `othelia` (backend `Othelia.Api` + frontend Nuxt) serta rencana improvement, fitur yang harusnya ada, dan fitur bernilai tinggi. Disusun 2026-08-06.

---

## Ringkasan kondisi saat ini

- **Backend**: ASP.NET Core net10, dual-listener Kestrel (`:5007` query API + `:4318` OTLP receiver). Menerima **traces + logs** via OTLP/HTTP protobuf & JSON, simpan ke SQL Server. **Metrics diterima tapi dibuang (no-op)**.
- **Frontend**: Nuxt 4, client-side fetching ke `/api` (proxy dev ke `:5007`). Halaman real: Dashboard, Traces, Logs, Settings. Halaman **mock**: Service Map, Metrics, Collectors, Alerts (placeholder).
- Auth: cookie sederhana (user statis di appsettings, plaintext).

---

## A. Bug & masalah kebenaran (harus diperbaiki)

1. **`DATEDIFF(MICROSECOND, …)` overflow** — `backend/src/Othelia.Api/Observability/TelemetryStore.cs:107,444`. Durasi trace & P50/P99 memakai `DATEDIFF` (return `int`, maks ~35,8 menit). Trace > 35 menit → query lempar exception. Fix: `DATEDIFF_BIG(MICROSECOND, …)`.
2. **State-key collision `'logs'`** — `frontend/app/composables/useTracing.ts:39` (seed mock `allLogsData`) vs `frontend/app/composables/useLogs.ts:15`. Sebelum `refresh()` selesai, halaman Logs bisa render entry mock ber-field salah tipe. Fix: ganti key `useLogs` → `logsList`.
3. **Settings `Query.*` tidak berefek** — `PUT /api/settings` tersimpan, tapi `TracingService`/`DashboardService` inject `IOptions<TracingOptions>` **statis** (`Services/TracingService.cs:15-19`, `Services/DashboardService.cs:21-25`). Hanya Ingestion & Retention yang live. Fix: kedua service pakai `ITracingOptionsResolver`.
4. **Status `slow` tidak pernah tercapai** — `frontend/app/utils/traceMapper.ts:36` `toStatus` hanya menghasilkan `error`/`ok`; filter "Slow" di traces selalu kosong; setting `slowThresholdMs` tak dipakai FE.
5. **`halfDur` salah untuk desimal** — `frontend/app/utils/traceFormat.ts:15` `parseInt('1.2s')` → 1 → tick tengah 500ms (harus 600ms).
6. **`setRange` tidak reset `filter.to`** — `frontend/app/pages/traces/index.vue:74`; pindah ke range lebih pendek menyisakan `to` lama. `to` juga tidak pernah di-set.
7. **`Math.Clamp` bisa throw** — `Services/TracingService.cs:45,91` jika `MaxTracesPerRequest < 1`; `PUT /api/settings` tanpa validasi menerima 0.
8. **Retention delete tanpa batching** — `DeleteSpansOlderThanAsync`/`DeleteLogsOlderThanAsync` satu `DELETE` besar (long lock) di tabel besar.
9. **`GET /api/traces/{id}/spans` tanpa cap** — `TelemetryStore.cs:361`; trace raksasa mengembalikan semua baris.
10. **LineChart tidak clear saat data kosong** — `frontend/app/components/LineChart.vue:91`; chart tetap tampilkan stale series.
11. **TraceDetail selalu kosong untuk data real**: `resourceAttrs` selalu `[]` (`traceMapper.ts:201`), tab Logs selalu kosong (`:202`), span `links` tidak ada di `ApiSpan` (`utils/otelTypes.ts`). `serviceCount || 3` hardcoded (`components/TraceDetail.vue:63`).
12. **`CHAR(32)`/`CHAR(16)` pad** — `Observability/SchemaInitializer.cs:34-35,73-74`; ID tidak standar → space-padded → join/equality gagal.
13. **Converter drop Array/Kvlist/Bytes** — `Observability/OtlpTraceConverter.cs:96`, `OtlpLogConverter.cs:84`; attribute nested hilang diam-diam.
14. **Info/DEBUG log dibuang di ingest** (`OtlpLogConverter.cs:35`) — by design tapi tak terdokumentasi di settings.

---

## B. Keamanan (penting)

1. **OTLP `/v1/*` tanpa auth, tanpa rate limit, tanpa body-size cap** — `OtlpEndpoints.cs:17-24`. Siapa pun di port 4318 bisa inject data/DoS. Body di-buffer penuh ke memori + **gzip bomb** tak dimitigasi (`:156-165`).
2. **Kredensial plaintext default `admin/admin123`** di `appsettings.json` + **ditampilkan di halaman login** (`Pages/Account/Login.cshtml:55`). Tanpa hashing, lockout, atau throttle.
3. **Cookie tanpa `Secure` + tidak ada HTTPS redirect** (`Program.cs`). Kredensial cookie dikirim clear-text di HTTP.
4. **CORS `AllowCredentials` + `AnyMethod`/`AnyHeader`** untuk `localhost:3000` + PUT/DELETE `/api/settings` tanpa CSRF token.
5. **`/api/health` selalu "healthy"** — tidak cek koneksi DB (`Controllers/HealthController.cs`).

---

## C. Fitur inti yang "harusnya ada" (belum ada)

1. **Metrics OTLP → storage + query** — `/v1/metrics` saat ini **no-op** (`OtlpEndpoints.cs:75`). Gap terbesar. Perlu: tabel metrics, agregasi per bucket, endpoint `/api/metrics/query`. Halaman Metrics (`pages/metrics/index.vue`) masih mock.
2. **Alerts real + ack** — `Acknowledged` selalu `false`, ack hanya local state (`composables/useDashboard.ts:92`). Perlu: tabel alert, `GET /api/alerts`, `PATCH /api/alerts/{id}/ack`. Halaman `pages/alerts.vue` placeholder statis.
3. **Service Map real** — `pages/services/index.vue` masih mock (rantai `→` palsu). Perlu endpoint dependency graph dari pasangan parent/child service (`/api/service-map`).
4. **Collectors real** — `pages/collectors/index.vue` mock; perlu endpoint status per service (last-seen/health).
5. **Tab Logs di TraceDetail** — API `/api/logs?traceId=` sudah ada, tapi tab selalu kosong. Tinggal wire.
6. **Resource attributes & span links** ditampilkan — perlu field baru di `ApiSpan` + API return.
7. **Slow threshold dipakai FE** — mapper pakai `slowThresholdMs` agar status/filter slow berfungsi.

---

## D. Fitur bernilai tinggi (sangat membantu)

1. **Live update via SSE** — dashboard polling `3 request/detik` (`pages/index.vue:20`). SSE/WebSocket untuk traces+dashboard lebih ringan & real-time.
2. **Export trace beneran** — tombol `↑ Export` di `components/AppHeader.vue:28` cuma toast. Implement: export trace JSON/OTLP per trace, atau CSV untuk list.
3. **Filter attribute tag** (`key=value`) di traces — sekarang hanya name/path. Query `AttributesJson`.
4. **Dedicated `/metrics` (Prometheus format)** — agar bisa di-scrape, di luar dashboard.
5. **Auth roles + API token** — admin/viewer; token OTLP (`X-Otlp-Token`) untuk receiver; halaman kelola token.
6. **OTLP gRPC (:4317)** — banyak eksporter default gRPC; sekarang hanya HTTP protobuf.
7. **IngestionFilter untuk logs** — sekarang filter hanya spans (`OtlpEndpoints.cs:46` vs `:112`); konsistensi config.
8. **Klik-untuk-filter** — klik service node di dashboard/waterfall → set global service switcher (infra sudah ada).
9. **Distribusi error/duration chart per service** (histogram), bukan hanya P50/P99.
10. **Sample rate control + dropped-attrs/flags counts** di ingest agar data loss terlihat.

---

## E. Frontend cleanup (kualitas)

- Hapus ~500 baris mock mati (`mockTraces`, `dashboardMetricsData`, `throughputTimeSeries`, `serviceHealthData`, `recentAlertsData`) + export mati (`loadServices`, `serviceList`, `allLogs`, `ApiThroughputSeries`).
- Sidebar badges: **Logs badge** = hitung error-span (`components/AppSidebar.vue:44`) → harusnya total log dari `/api/logs` (X-Total-Count); Collectors `'3'`/Alerts `'2'` hardcoded.
- Duplikasi `statusColor`/`statusCodeClass` (`pages/index.vue` vs `utils/traceFormat.ts`); `.dmc-down` hijau (harus merah); CSS mati `.throughput-chart/.tp-*/.loading-overlay`.
- `runtimeConfig.apiBase` didefinisikan tapi tidak pernah dibaca — pakai atau hapus. Produksi butuh `routeRules` proxy untuk `/api` & `/account` (devProxy hanya dev).

---

## Roadmap prioritas

| Fase | Item | Dampak |
|---|---|---|
| **1 — Perbaikan** | `DATEDIFF_BIG`, fix state key `logs`, settings `Query.*` via resolver, `halfDur`, status slow, reset `to`, validasi clamp, cap spans, retain batching, body-size cap + basic OTLP token, hapus kredensial tampil, health cek DB | Stabilitas & aman |
| **2 — Fitur inti** | Metrics OTLP tersimpan+query, Alerts real+ack, Service Map, Collectors, tab Logs trace, resource/links, slow threshold | Melengkapi "yang harusnya ada" |
| **3 — Bernilai tinggi** | SSE live, export real, tag filter, roles/token, OTLP gRPC, prometheus, log ingestion filter | Produk jadi matang |
