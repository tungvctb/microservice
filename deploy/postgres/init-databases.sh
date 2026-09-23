#!/bin/bash
# Database-per-service: mỗi service sở hữu schema riêng, không service nào đọc bảng của service khác.
# Ở production nên tách hẳn thành nhiều Postgres instance; ở đây gom 1 container cho nhẹ máy dev.
set -e

for db in identity_db product_db inventory_db order_db payment_db notification_db; do
  echo "Tạo database $db..."
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    SELECT 'CREATE DATABASE $db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOSQL
done

echo "Đã khởi tạo xong toàn bộ database."
