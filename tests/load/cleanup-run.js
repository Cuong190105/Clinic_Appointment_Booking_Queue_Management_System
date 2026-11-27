// Cancels appointments left behind by load runs whose teardown did not finish (e.g. aborted or a
// network error during teardown). Matches every non-terminal appointment whose reason contains
// CLEANUP_RUN_PREFIX (default "spike-") on the given day offsets.
//
//   ./tests/load/run-k6.ps1 cleanup-run.js -Env @{ CLEANUP_RUN_PREFIX='spike-'; CLEANUP_DAY_OFFSETS='3,4' }
import { ADMIN_PASSWORD, ADMIN_USERNAME, clinicDate } from './config.js';
import { cleanupRunAppointments } from './lib/clinic.js';
import { mustLogin } from './lib/session.js';

const PREFIX = __ENV.CLEANUP_RUN_PREFIX || 'spike-';
const OFFSETS = String(__ENV.CLEANUP_DAY_OFFSETS || '0,1,2,3,4,5,6,7').split(',').map(Number);

export const options = { vus: 1, iterations: 1 };

export default function () {
  const admin = mustLogin({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD });
  const result = cleanupRunAppointments(admin, OFFSETS.map((d) => clinicDate(d)), PREFIX);
  console.log(`[CLEANUP] prefix=${PREFIX} days=${OFFSETS.join(',')} cancelled=${result.cancelled} failed=${result.failed}`);
}
