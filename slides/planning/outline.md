# Presentation outline

## Scope and source policy

- Spoken presentation: preserve the approved 16-slide, 15-minute order and message from [`docs/PRESENTATION_SCRIPT.md`](../../docs/PRESENTATION_SCRIPT.md).
- PowerPoint deck: 16 spoken slides plus one unspoken references appendix (slide 17). The appendix is outside the 15-minute delivery.
- Detailed wording remains in the original script and is also added to speaker notes. Slide copy is shortened only for readability.
- Benchmark comparisons keep their workload and evidence caveats visible. No new measurements or claims are introduced.

## Narrative arc

| Slide | Section | Main takeaway | Runtime |
|---:|---|---|---:|
| 1 | Project and goal | The project moves a clinic workflow from a monolith to runnable, tested services. | 0:40 |
| 2 | Users and workflows | Four roles participate in connected booking, check-in, care, and notification journeys. | 0:55 |
| 3 | Legacy architecture | The old app used one Clinic API process and one shared database context. | 0:50 |
| 4 | Legacy data model | Shared relational data made cross-domain queries easy but ownership broad. | 1:00 |
| 5 | Motivation | Service boundaries clarify ownership, while adding distributed-systems costs. | 0:40 |
| 6 | Service boundaries | Six domain services own their workflows and data behind a Gateway. | 1:00 |
| 7 | New architecture | Synchronous APIs and RabbitMQ events connect the Gateway and service owners. | 1:00 |
| 8 | Booking coordination | Appointment orchestrates a SAGA with a synchronous slot reservation and local outbox. | 1:15 |
| 9 | Queue coordination | Check-in events drive an idempotent Queue consumer and downstream choreography. | 1:00 |
| 10 | Contracts and operations | OpenAPI, drift checks, tests, health, metrics, and tracing support the running system. | 0:50 |
| 11 | Reliability targets | SLI/SLO/QoS targets define how the project measures latency, failures, events, and invariants. | 0:45 |
| 12 | Benchmark method | Booking stress and mixed workload answer different performance questions. | 0:55 |
| 13 | Optimization history | Iteration improved some results, but a resource-balanced run still exposed a p95 miss. | 1:05 |
| 14 | Latest microservice results | High-load and mixed profiles meet their declared gates with a small number of unclassified peak iterations. | 1:10 |
| 15 | Monolith comparison | Current paired booking runs show tradeoffs; mixed clinic-peak exposed a monolith walk-in defect. | 1:00 |
| 16 | Conclusion | The project meets its implementation goals; equivalent repeated comparison work remains. | 0:55 |
| 17 | References (appendix) | Source documents and test evidence behind the presentation. | Not spoken |

**Total spoken time: 15:00.**

## Visual rhythm

- Slides 1–2: typographic opening and role journey.
- Slides 3–4: legacy architecture and conceptual ERD, contrasting a simple stack with a shared data model.
- Slides 5–7: rationale, ownership map, and deployment architecture.
- Slides 8–9: two distinct coordination patterns, with editable sequence/flow diagrams.
- Slides 10–12: engineering evidence, SLO scorecard, and workload framing.
- Slides 13–15: timeline and data visuals; slide 15 combines a native chart with a compact results table.
- Slides 16–17: concise conclusion and a quiet references appendix.

## Design approval checkpoint

The first review deck contains representative slides 3, 8, and 15. These test the legacy diagram, SAGA diagram, and benchmark comparison treatment. The complete deck should be implemented only after the user approves this visual direction or requests changes.
