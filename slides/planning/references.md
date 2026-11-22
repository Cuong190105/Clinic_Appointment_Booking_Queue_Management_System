# References and evidence

## Citation convention

Short labels appear in slide footers and map to the sources below. Internal project documents are cited rather than adding outside claims. The approved script is the authority for the spoken narrative; the project report, design/task documents, and sanitized benchmark artifacts support technical claims.

## Sources

| Label | Source | Use |
|---|---|---|
| [S] | [`docs/PRESENTATION_SCRIPT.md`](../../docs/PRESENTATION_SCRIPT.md) | Approved slide order, narration, timing, and benchmark caveats. |
| [P] | [`docs/PROJECT_REPORT.md`](../../docs/PROJECT_REPORT.md) | System scope, monolith architecture/ERD, service ownership, architecture, workflows, OpenAPI and project status. |
| [Q] | [`tasks/SLI_SLO_MATRIX_MEMBER_A.md`](../../tasks/SLI_SLO_MATRIX_MEMBER_A.md) | SLI/SLO definitions, QoS and measurement conventions. |
| [B] | [`tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md`](../../tasks/PHASE_9_LOAD_TEST_REPORT_MEMBER_A.md), especially §§19, 21–22 | Microservice high-load/mixed results and monolith comparison, test method and caveats. |
| [C] | [`tasks/contracts/`](../../tasks/contracts/) and [`backend/Clinic.IntegrationTests/ApiContractDriftTests.cs`](../../backend/Clinic.IntegrationTests/ApiContractDriftTests.cs) | OpenAPI contract snapshots and drift-test implementation. |
| [E1] | [`tests/load/evidence/highload-monolith-2026-11-22.json`](../../tests/load/evidence/highload-monolith-2026-11-22.json) and run artifacts `highload-monolith-2026-11-22-run-0126.json`, `-0131.json`, `-0139.json` | Sanitized accepted monolith high-load runs. Run `0128` is excluded from the median, as documented in [B]. |
| [E2] | [`tests/load/evidence/mixed-normal-monolith-2026-11-22.json`](../../tests/load/evidence/mixed-normal-monolith-2026-11-22.json) and [`mixed-clinic-peak-monolith-2026-11-22.json`](../../tests/load/evidence/mixed-clinic-peak-monolith-2026-11-22.json) | Sanitized monolith mixed-workload evidence. |
| [E3] | [`tests/load/evidence/highload-micro-2026-11-17-post-reserve-optimization.json`](../../tests/load/evidence/highload-micro-2026-11-17-post-reserve-optimization.json), [`mixed-normal-2026-11-17.json`](../../tests/load/evidence/mixed-normal-2026-11-17.json), and [`mixed-clinic-peak-2026-11-17.json`](../../tests/load/evidence/mixed-clinic-peak-2026-11-17.json) | Sanitized microservice benchmark evidence. |

## Citation placement

- Slides 3–4, 6–9, 10: [P], with [C] on slide 10.
- Slide 11: [Q].
- Slides 12–15: [B], [E1], [E2], [E3] as applicable.
- Slides 1–2, 5, 16: [S], [P].
- Appendix slide 17: complete list above.

## Interpretation constraints

- The benchmark values are Docker Compose project measurements, not production SLA commitments.
- High-load accepted medians use three runs per system; a monolith run with teardown timeout is excluded.
- Mixed normal has one run per system. Clinic-peak mixes differ in journey totals and the monolith walk-in path failed; do not compare its Queue latency/capacity with a successful Queue run.
- The two stacks used persistent databases that were not restored from an identical snapshot before every run. Treat the measurements as observed runs, not a controlled architecture-wide superiority claim.
- No external images or third-party research are planned; diagrams and charts are native editable PowerPoint elements.
