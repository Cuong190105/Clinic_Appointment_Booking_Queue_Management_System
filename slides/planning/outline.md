# Presentation outline

## Scope and source policy

- Spoken presentation: 17 slides in 15 minutes, following [`docs/PRESENTATION_SCRIPT.md`](../../docs/PRESENTATION_SCRIPT.md). Revision 27/11/2026 replaced slide 11 with a per-service SLA/SLO/QoS table and added slide 12 (QoS verified on the running system); later slides shifted by one and other timings were shortened to keep 15:00.
- PowerPoint deck: 17 spoken slides plus one unspoken references appendix (slide 18). The appendix is outside the 15-minute delivery. The live deck has the 17 spoken slides only.
- Detailed wording remains in the original script and is also added to speaker notes. Slide copy is shortened only for readability.
- Benchmark comparisons keep their workload and evidence caveats visible. No new measurements or claims are introduced.

## Narrative arc

| Slide | Section | Main takeaway | Runtime |
|---:|---|---|---:|
| 1 | Project and goal | The project moves a clinic workflow from a monolith to runnable, tested services. | 0:40 |
| 2 | Users and workflows | Four roles participate in connected booking, check-in, care, and notification journeys. | 0:45 |
| 3 | Legacy architecture | The old app used one Clinic API process and one shared database context. | 0:40 |
| 4 | Legacy data model | Shared relational data made cross-domain queries easy but ownership broad. | 0:50 |
| 5 | Motivation | Service boundaries clarify ownership, while adding distributed-systems costs. | 0:40 |
| 6 | Service boundaries | Six domain services own their workflows and data behind a Gateway. | 1:00 |
| 7 | New architecture | Synchronous APIs and RabbitMQ events connect the Gateway and service owners. | 1:00 |
| 8 | Booking coordination | Appointment orchestrates a SAGA with a synchronous slot reservation and local outbox. | 1:15 |
| 9 | Queue coordination | Check-in events drive an idempotent Queue consumer and downstream choreography. | 1:00 |
| 10 | Contracts and operations | OpenAPI, drift checks, tests, health, metrics, and tracing support the running system. | 0:45 |
| 11 | Per-service SLA/SLO/QoS | Each service has a tier, availability, latency/workflow target, QoS mechanism and RPO/RTO. | 1:00 |
| 12 | QoS verification | QoS policy, fault isolation, login capacity, spike, restore drill and invariants were measured on the running stack. | 1:00 |
| 13 | Benchmark method | Booking stress and mixed workload answer different performance questions. | 0:45 |
| 14 | Optimization history | Iteration improved some results, but a resource-balanced run still exposed a p95 miss. | 0:50 |
| 15 | Latest microservice results | High-load and mixed profiles meet their declared gates with a small number of unclassified peak iterations. | 1:00 |
| 16 | Monolith comparison | Current paired booking runs show tradeoffs; mixed clinic-peak exposed a monolith walk-in defect. | 1:00 |
| 17 | Conclusion | The project meets its implementation goals; equivalent repeated comparison work remains. | 0:50 |
| 18 | References (appendix) | Source documents and test evidence behind the presentation. | Not spoken |

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
