import http from "k6/http";
import { check, sleep } from "k6";
import {
    BASE_URL,
    DEFAULT_HEADERS,
    login,
    getAuthHeaders,
    getCachedTokens,
} from "./config.js";

http.setResponseCallback(http.expectedStatuses(200, 429));

export const options = {
    stages: [
        { duration: "5s", target: 5 }, // Warm-up
        { duration: "10s", target: 50 }, // Sudden spike
        { duration: "20s", target: 50 }, // Hold peak
        { duration: "10s", target: 5 }, // Fast scale-down
        { duration: "5s", target: 0 }, // Recovery
    ],
    thresholds: {
        // Under extreme surge, rate limiting (429) is expected, but 5xx crashes should be zero (<1%)
        http_req_failed: ["rate<0.01"],
        http_req_duration: ["p(95)<1000"],
    },
};

export function setup() {
    const cached = getCachedTokens();
    if (cached && cached.patients && cached.patients.length >= 20) {
        return { tokens: cached.patients.slice(0, 20).map((p) => p.token) };
    }
    const tokens = [];
    for (let i = 1; i <= 20; i++) {
        tokens.push(login(`patient${i}`));
    }
    return { tokens };
}

export default function (data) {
    const vuIndex = (__VU - 1) % data.tokens.length;
    const token = data.tokens[vuIndex];

    // Alternating between Specialties and Doctors
    const endpoint =
        __ITER % 2 === 0 ? `${BASE_URL}/specialties` : `${BASE_URL}/doctors`;
    const res = http.get(endpoint, {
        headers: token
            ? getAuthHeaders(token, null, `spike-${__VU}-${__ITER}`)
            : {
                  ...DEFAULT_HEADERS,
                  "X-Correlation-ID": `spike-${__VU}-${__ITER}`,
              },
    });

    check(res, {
        "handled cleanly (200 OK or 429 Rate Limited)": (r) =>
            r.status === 200 || r.status === 429,
        "no server crash (status < 500)": (r) => r.status < 500,
    });

    sleep(0.1);
}
