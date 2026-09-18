#!/bin/sh
# Espera a que terminen los runs de GitHub Actions de un commit y lista sus jobs.
# Uso:  sh tools/agent/wait-ci.sh <rama> <sha7> [intentos=38]     (un intento cada 15 s)
# Un verde no basta: leer despues el log de los jobs (gh.sh LOGS <job_id>) y comprobar que los tests nuevos SE EJECUTARON.
S=$(dirname "$0")
TRIES=${3:-38}
for i in $(seq 1 "$TRIES"); do
  OUT=$(sh "$S/gh.sh" GET "/actions/runs?branch=$1&per_page=20" | python -c "
import sys,json
runs=[r for r in json.load(sys.stdin)['workflow_runs'] if r['head_sha'].startswith('$2')]
for r in runs: print(r['id'],r['name'],r['status'],r['conclusion'])
print('DONE' if runs and all(r['status']=='completed' for r in runs) else 'WAIT')")
  echo "$OUT" | tail -1 | grep -q DONE && break
  sleep 15
done
echo "$OUT"
for RUN in $(echo "$OUT" | awk '$NF!="DONE" && $NF!="WAIT"{print $1}' | tr -d '\r'); do
  sh "$S/gh.sh" GET "/actions/runs/$RUN/jobs" | python -c "
import sys,json
for j in json.load(sys.stdin)['jobs']:
    bad=[s['name'] for s in j['steps'] if s['conclusion'] not in ('success','skipped',None)]
    print('  job',j['id'],j['name'],j['conclusion'],('FALLA EN: '+'; '.join(bad)) if bad else '')"
done
