#!/bin/sh
# API de GitHub sin gh CLI. El token sale de `git credential fill` en el momento y nunca se imprime ni se guarda.
# Uso:  sh tools/agent/gh.sh GET  /pulls/48
#       sh tools/agent/gh.sh POST /pulls cuerpo.json      (cuerpo SIEMPRE desde fichero UTF-8, nunca inline)
#       sh tools/agent/gh.sh LOGS <job_id>                (log completo de un job de Actions)
REPO="Rivadesa/Costi-a"
TOKEN=$(printf 'protocol=https\nhost=github.com\n\n' | git credential fill | sed -n 's/^password=//p')
if [ "$1" = "LOGS" ]; then
  JOB=$(printf '%s' "$2" | tr -d '\r\n')
  curl -sSL -H "Authorization: Bearer $TOKEN" "https://api.github.com/repos/$REPO/actions/jobs/$JOB/logs"
elif [ -n "$3" ]; then
  curl -sS -X "$1" -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.github+json" \
    -H "Content-Type: application/json; charset=utf-8" --data-binary "@$3" "https://api.github.com/repos/$REPO$2"
else
  curl -sS -X "$1" -H "Authorization: Bearer $TOKEN" -H "Accept: application/vnd.github+json" "https://api.github.com/repos/$REPO$2"
fi
