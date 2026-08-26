#!/usr/bin/env bash
# Smoke-tests the ProjectHub API's read surface. Re-authenticates on every run because access tokens
# live 15 minutes, which is shorter than an interactive debugging session.
set -u

API="${API:-http://localhost:5216}"
CRED_FILE=/tmp/ph_smoke_cred.json

if [ ! -f "$CRED_FILE" ]; then
  EMAIL="smoke$(date +%s)@projecthub.local"
  curl -s -X POST "$API/api/auth/register" -H "Content-Type: application/json" \
    -d "{\"email\":\"$EMAIL\",\"firstName\":\"Smoke\",\"lastName\":\"Test\",\"password\":\"Passw0rd!23\"}" > /dev/null
  printf '{"email":"%s","password":"Passw0rd!23"}' "$EMAIL" > "$CRED_FILE"
  echo "registered $EMAIL"
fi

TOKEN=$(curl -s -X POST "$API/api/auth/login" -H "Content-Type: application/json" \
  --data-binary @"$CRED_FILE" | python -c "import json,sys;print(json.load(sys.stdin)['accessToken'])")

if [ -z "$TOKEN" ]; then echo "LOGIN FAILED"; exit 1; fi

# Ensure there is something to find.
PID=$(curl -s -X POST "$API/api/projects" -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Apollo Launch","description":"Ship the launch checklist"}' \
  | python -c "import json,sys;print(json.load(sys.stdin).get('id',''))")

if [ -n "$PID" ]; then
  curl -s -o /dev/null -X POST "$API/api/projects/$PID/tasks" -H "Content-Type: application/json" \
    -H "Authorization: Bearer $TOKEN" \
    -d '{"title":"Write launch runbook","description":"Cover rollback steps","priority":3}'
fi

check() {
  local label="$1" url="$2"
  local code
  code=$(curl -s -o /tmp/ph_smoke_body.txt -w "%{http_code}" "$API$url" -H "Authorization: Bearer $TOKEN")
  if [ "$code" = "200" ]; then
    printf '  \033[32mPASS\033[0m %-34s %s\n' "$label" "$code"
  else
    printf '  \033[31mFAIL\033[0m %-34s %s  %s\n' "$label" "$code" "$(head -c 160 /tmp/ph_smoke_body.txt)"
  fi
}

echo "project id: ${PID:-<none>}"
echo
echo "PROJECTS"
check "list (no filters)"        "/api/projects?pageNumber=1&pageSize=5"
check "list searchTerm"          "/api/projects?pageNumber=1&pageSize=5&searchTerm=Apollo"
check "list sortBy=Name"         "/api/projects?pageNumber=1&pageSize=5&sortBy=Name"
check "list sortBy=Status"       "/api/projects?pageNumber=1&pageSize=5&sortBy=Status"
check "list sortBy=CreatedAt"    "/api/projects?pageNumber=1&pageSize=5&sortBy=CreatedAt"
check "list status=Active"       "/api/projects?pageNumber=1&pageSize=1&status=Active"
check "get by id"                "/api/projects/$PID"

echo
echo "TASKS"
check "list (no filters)"        "/api/projects/$PID/tasks?pageNumber=1&pageSize=5"
check "list searchTerm"          "/api/projects/$PID/tasks?pageNumber=1&pageSize=5&searchTerm=runbook"
check "list sortBy=Title"        "/api/projects/$PID/tasks?pageNumber=1&pageSize=5&sortBy=Title"
check "list sortBy=Priority"     "/api/projects/$PID/tasks?pageNumber=1&pageSize=5&sortBy=Priority"
check "list status=Todo"         "/api/projects/$PID/tasks?pageNumber=1&pageSize=5&status=Todo"
check "list priority=High"       "/api/projects/$PID/tasks?pageNumber=1&pageSize=5&priority=High"

echo
echo "OTHER"
check "members"                  "/api/projects/$PID/members"
check "search q"                 "/api/search?q=launch&pageNumber=1&pageSize=20"
check "notifications"            "/api/notifications?pageNumber=1&pageSize=5"
check "notifications unreadOnly" "/api/notifications?pageNumber=1&pageSize=1&unreadOnly=true"
check "audit logs (Project)"     "/api/auditlogs/Project/$PID?pageNumber=1&pageSize=5"
