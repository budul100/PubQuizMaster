#!/usr/bin/env bash
set -euo pipefail

APPNAME="pubquizmaster"
DB_SERVICE="pubquizmaster-db"
DB_USER="pubquizmaster"
DB_NAME="pubquizmaster"

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DB_FILENAME="${APPNAME}_${TIMESTAMP}.sql.gz"

HIDRIVE_PATH="hidrive:users/xyz/Backups/${APPNAME}"
BACKUP_DIR="/var/backups/pubquiz"
PROJECT_DIR="/opt/${APPNAME}"

mkdir -p "$BACKUP_DIR"

# Load env vars (DB_PASSWORD etc.)
set -a
source "$PROJECT_DIR/.env"
set +a

echo "=== Running database backup ==="
docker compose --project-directory "$PROJECT_DIR" exec -T "$DB_SERVICE" \
    pg_dump -U "$DB_USER" "$DB_NAME" \
    | gzip > "$BACKUP_DIR/$DB_FILENAME"

if ! gzip -dc "$BACKUP_DIR/$DB_FILENAME" | tail -n 5 | grep -q "PostgreSQL database dump complete"; then
    echo "ERROR: database dump incomplete or empty" >&2
    rm -f "$BACKUP_DIR/$DB_FILENAME"
    exit 1
fi

echo "=== Uploading to HiDrive ==="
rclone copy "$BACKUP_DIR/$DB_FILENAME" "$HIDRIVE_PATH/"

echo "=== Cleaning up local backups older than 7 days ==="
find "$BACKUP_DIR" -name "${APPNAME}_*.sql.gz" -mtime +7 -delete

echo "=== Cleaning up remote backups (keep last 3) ==="
rclone lsf "$HIDRIVE_PATH/" --files-only --include "${APPNAME}_*.sql.gz" \
    | sort \
    | head -n -3 \
    | while read -r file; do
        rclone deletefile "$HIDRIVE_PATH/$file"
    done

echo "=== Done: $DB_FILENAME ==="