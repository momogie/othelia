#!/usr/bin/env bash
#
# Othelia deploy script (idempotent) + mode remove.
# BE & FE sudah di-publish lokal lalu di-upload manual ke server.
#
# Usage:
#   sudo bash install.sh                        # install interaktif (prompt be, fe, port)
#   sudo bash install.sh <be> <fe> [port]       # install langsung dari argumen
#   sudo bash install.sh remove                 # hapus instalasi + config nginx/systemd
#
# Contoh:
#   sudo bash install.sh /home/user/othelia-be /home/user/othelia-fe 8080
#
# Prasyarat di server: .NET 10 runtime, nginx, user sistem 'othelia' (dibuat otomatis).
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

APP_USER="othelia"
APP_HOME="/opt/othelia"
API_DST="$APP_HOME/api"
NGINX_DST="/etc/nginx/conf.d/othelia.conf"
UNIT_SRC="$SCRIPT_DIR/othelia-api.service"
UNIT_DST="/etc/systemd/system/othelia-api.service"

# ─────────────────────────────── REMOVE ──────────────────────────────────
remove() {
    echo "Menghapus othelia..."

    # systemd unit
    systemctl disable --now othelia-api 2>/dev/null || true
    rm -f "$UNIT_DST"
    systemctl daemon-reload

    # nginx
    rm -f "$NGINX_DST"
    if command -v nginx >/dev/null 2>&1; then
        nginx -t
        systemctl reload nginx
    fi

    # data (ditanya dulu, default: simpan)
    read -rp "Hapus juga $APP_HOME dan user '$APP_USER'? [y/N] " confirm
    if [[ "$confirm" =~ ^[Yy]$ ]]; then
        rm -rf "$APP_HOME"
        userdel "$APP_USER" 2>/dev/null || true
        echo "Data & user dihapus."
    else
        echo "Artifact dibiarkan di $APP_HOME."
    fi
    echo "OK. Othelia tidak lagi aktif di nginx/systemd."
}

case "${1:-}" in
    remove|--remove|-r) remove; exit 0 ;;
esac

# ── minta path secara interaktif kalau tidak diberikan via argumen ─────────
prompt_path() {
    local var_name="$1" label="$2" marker="$3"
    local value
    while :; do
        read -rp "Masukkan path folder $label: " value
        if [ -z "$value" ]; then
            echo "Path tidak boleh kosong."
            continue
        fi
        if [ ! -d "$value" ]; then
            echo "ERROR: '$value' bukan folder yang ada."
            continue
        fi
        if [ ! -f "$value/$marker" ]; then
            echo "ERROR: '$value' tidak berisi $marker."
            continue
        fi
        eval "$var_name='$(cd "$value" && pwd)'"
        return 0
    done
}

BE_SRC="${1:-}"
FE_SRC="${2:-}"

if [ -z "$BE_SRC" ]; then
    prompt_path BE_SRC "BE publish (berisi Othelia.Api.dll)" "Othelia.Api.dll"
else
    BE_SRC="$(cd "$BE_SRC" && pwd)"
fi

if [ -z "$FE_SRC" ]; then
    prompt_path FE_SRC "FE static (berisi index.html)" "index.html"
else
    FE_SRC="$(cd "$FE_SRC" && pwd)"
fi

# ── port nginx untuk web (default 8080) ───────────────────────────────────
WEB_PORT="${3:-}"
if [ -z "$WEB_PORT" ]; then
    while :; do
        read -rp "Masukkan port nginx untuk web [8080]: " WEB_PORT
        WEB_PORT="${WEB_PORT:-8080}"
        if [[ "$WEB_PORT" =~ ^[0-9]+$ ]] && [ "$WEB_PORT" -ge 1 ] && [ "$WEB_PORT" -le 65535 ]; then
            break
        fi
        echo "ERROR: port harus angka 1-65535."
    done
fi

# ── sanity check ──────────────────────────────────────────────────────────
[ -f "$BE_SRC/Othelia.Api.dll" ] || { echo "ERROR: '$BE_SRC' bukan folder publish backend (Othelia.Api.dll tidak ada)"; exit 1; }
[ -f "$FE_SRC/index.html" ]     || { echo "ERROR: '$FE_SRC' bukan folder static frontend (index.html tidak ada)"; exit 1; }

# ── user system (idempotent) ──────────────────────────────────────────────
if ! id "$APP_USER" >/dev/null 2>&1; then
    useradd --system --user-group --home "$APP_HOME" --shell /usr/sbin/nologin "$APP_USER"
    echo "user $APP_USER dibuat"
fi
mkdir -p "$API_DST"

# ── salin BE ke /opt/othelia/api ──────────────────────────────────────────
rsync -a --delete "$BE_SRC/" "$API_DST/" || cp -rf "$BE_SRC/." "$API_DST/"

# FE TIDAK dipindah — nginx serve langsung dari folder FE yang di-input.
WEB_ROOT="$FE_SRC"
echo "FE di-serve langsung dari: $WEB_ROOT (tidak disalin ke /opt/othelia/web)"

# appsettings: pakai .example sebagai template kalau belum ada (tidak menimpa yg sudah ada)
EXAMPLE="$SCRIPT_DIR/../backend/src/Othelia.Api/appsettings.json.example"
if [ ! -f "$API_DST/appsettings.json" ] && [ -f "$EXAMPLE" ]; then
    cp "$EXAMPLE" "$API_DST/appsettings.json"
    echo "WARN: appsettings.json dibuat dari example — ISI placeholder (ConnectionString, Auth:Accounts, ApiKey)."
fi

chown -R "$APP_USER:" "$API_DST"

# ── systemd unit ──────────────────────────────────────────────────────────
cp "$UNIT_SRC" "$UNIT_DST"
systemctl daemon-reload
systemctl enable --now othelia-api

# ── nginx ─────────────────────────────────────────────────────────────────
# kalau port 80 dipilih, nonaktifkan default site agar tidak bentrok
if [ "$WEB_PORT" = "80" ]; then
    rm -f /etc/nginx/sites-enabled/default
fi

sed -e "s/__LISTEN_PORT__/$WEB_PORT/g" -e "s|__WEB_ROOT__|$WEB_ROOT|g" "$SCRIPT_DIR/nginx.conf" > "$NGINX_DST"
nginx -t
systemctl reload nginx

echo "OK. Cek: journalctl -u othelia-api -f  |  curl http://localhost:5007/api/health"
echo "  Web diakses via http://<IP-server>:$WEB_PORT/  (nginx serve $WEB_ROOT)"
