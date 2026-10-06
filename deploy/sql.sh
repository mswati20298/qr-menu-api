#!/usr/bin/env bash
# Opens a SQL prompt on the server's SQL Server, or runs one query:
#   ./sql.sh                                            interactive (type a query, then GO; exit with QUIT)
#   ./sql.sh "SELECT Name, Subdomain FROM Restaurants"  one query on QrMenuProd
#   ./sql.sh -d QrMenuDemo "SELECT COUNT(*) FROM Orders"
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ./.env; set +a

db=QrMenuProd
if [ "${1:-}" = "-d" ]; then db="$2"; shift 2; fi

args=(-S localhost -U sa -P "$SQL_SA_PASSWORD" -C -d "$db" -W -s " | ")
if [ $# -gt 0 ]; then
  docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd "${args[@]}" -Q "SET NOCOUNT ON; $*"
else
  docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd "${args[@]}"
fi
