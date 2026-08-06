# Othelia — Setup & Deployment

Panduan deploy Othelia di **Linux** (diasumsikan di server terpisah dari AppAPI). Terdiri dari:

1. Backend `.NET` (Othelia.Api) — OTLP receiver `:4318` + query API `:5007`.
2. Frontend Nuxt 4 — dashboard.
3. nginx — reverse proxy (HTTPS).
4. SQL Server — database `othelia`.

Referensi config: `backend/src/Othelia.Api/appsettings.json.example` dan `docs/architecture/README.md`.

---

## 1. Prasyarat

- **.NET 10 runtime** (backend).
- **Node.js 20+** (build frontend).
- **SQL Server** yang bisa diakses server ini; user harus punya hak **`CREATE TABLE`** (schema dibuat otomatis saat startup).
- **nginx**.
- **Firewall**: buka `4318` (dari server AppAPI), dan `443`/`5007` (dari nginx).

---

## 2. Backend (Othelia.Api)

### 2.1 Publish

```bash
cd backend
dotnet publish src/Othelia.Api/Othelia.Api.csproj -c Release -o /opt/othelia/api
```

### 2.2 Konfigurasi

```bash
cp src/Othelia.Api/appsettings.json.example /opt/othelia/api/appsettings.json
# isi placeholder
```

Nilai penting:

| Key | Keterangan |
|---|---|
| `Tracing:Receiver:Host` | **`"0.0.0.0"`** agar AppAPI di server lain bisa kirim OTLP. Nilai lain: `"localhost"`, atau IP spesifik. |
| `Tracing:Receiver:Port` | `4318` |
| `Tracing:Api:Host` / `Port` | `localhost:5007` (belakang nginx di host sama) |
| `Tracing:Storage:ConnectionString` | SQL Server production; user harus bisa `CREATE TABLE` |
| `Tracing:Retention:Enabled` | `true` + `RetentionDays` agar DB tidak membengkak |
| `Tracing:Ingestion:Endpoint` | alamat receiver (untuk info/UI), mis. `http://0.0.0.0:4318` |

### 2.3 systemd unit

`/etc/systemd/system/othelia-api.service`:

```ini
[Unit]
Description=Othelia Api (OTLP receiver + dashboard API)
After=network.target

[Service]
WorkingDirectory=/opt/othelia/api
ExecStart=/usr/bin/dotnet Othelia.Api.dll
Restart=always
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload
systemctl enable --now othelia-api
journalctl -u othelia-api -f   # cek log
```

### 2.4 Verifikasi receiver

```bash
# Health check
curl http://localhost:5007/api/health

# Receiver menolak tanpa body → 400 (artinya listener aktif)
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:4318/v1/traces
# → 400
```

---

## 3. Frontend (Nuxt)

### 3.1 Build

```bash
cd frontend
npm install
npx nuxt build
# hasil di .output/ (SSR server)
```

Cek `nuxt.config.ts` — `runtimeConfig.apiBase` (dipakai server-side fetch) harus menunjuk backend:

```ts
runtimeConfig: {
  apiBase: 'http://localhost:5007',
  public: { apiBase: '/api' },
}
```

> `devProxy` hanya untuk development. Di production `/api` di-route nginx ke `localhost:5007`.

### 3.2 systemd unit (SSR node server)

`/etc/systemd/system/othelia-web.service`:

```ini
[Unit]
Description=Othelia Nuxt dashboard
After=network.target

[Service]
WorkingDirectory=/opt/othelia/frontend/.output
ExecStart=/usr/bin/node server/index.mjs
Restart=always
Environment=NITRO_PORT=3000
Environment=NITRO_HOST=127.0.0.1

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload && systemctl enable --now otelia-web
```

---

## 4. nginx (reverse proxy + HTTPS)

```nginx
server {
    listen 443 ssl;
    server_name othelia.example.id;
    # ssl_certificate / ssl_certificate_key ...;

    # Query API backend
    location /api/ {
        proxy_pass http://127.0.0.1:5007/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # OpenAPI
    location /openapi/ {
        proxy_pass http://127.0.0.1:5007/openapi/;
        proxy_set_header Host $host;
    }

    # Dashboard Nuxt SSR
    location / {
        proxy_pass http://127.0.0.1:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
nginx -t && systemctl reload nginx
```

> **Penting**: port OTLP `:4318` **tidak perlu** diekspos publik — cukup terbuka untuk server AppAPI (firewall/security group).

---

## 5. Integrasi dengan AppAPI

Di server AppAPI (`upahr-backend-new`), set:

```json
"OpenTelemetry": {
  "Endpoint": "http://<host-othelia>:4318"
}
```

lalu pastikan:
- Server othelia bind `0.0.0.0:4318` (`Tracing:Receiver:Host`).
- Firewall server othelia membuka `4318` dari IP server AppAPI.

Verifikasi ujung-ke-ujung:

```bash
# di server AppAPI
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://<host-othelia>:4318/v1/traces
# → 400 (konek) atau 0/timeout (blokir)
```

Setelah AppAPI mengirim telemetry, cek dashboard / query:

```bash
curl "http://localhost:5007/api/traces?limit=5"
curl "http://localhost:5007/api/services"
```

---

## 6. Troubleshooting

| Gejala | Solusi |
|---|---|
| AppAPI tidak muncul di dashboard | Cek `OpenTelemetry.Endpoint` AppAPI, binding `Receiver.Host=0.0.0.0`, firewall `4318`. |
| Receiver tidak bisa dibuat tabel | Beri user SQL hak `CREATE TABLE` di DB `othelia`. |
| `/api/settings` balas 503 | Storage tidak terkonfigurasi (Noop mode) — isi `Tracing:Storage:ConnectionString`. |
| Data cepat membengkak | Aktifkan `Tracing:Retention` (atau atur via UI Settings). |
| Dashboard kosong | AppAPI belum kirim atau window dashboard (`DashboardWindowSeconds`) belum berisi data. |
| Exporter log spam di AppAPI saat othelia mati | Set `OpenTelemetry.Exporter.OpenTelemetryProtocol` = `Error` di appsettings AppAPI. |
