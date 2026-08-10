# AStudio desktop roadmap

**Current wave:** S3 — Architecture domain modules  
**Updated:** 2026-08-10  
Upstream tracker: esti [ROADMAP.md](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/ROADMAP.md) D-waves.  
Open source until SaaS licensing is decided.

## Waves

| Wave | Outcome | Status |
| --- | --- | --- |
| S0 | Repo scaffold + pin AQC engine commit | **Done** — `vendor/AQC` @ `aorms-bridge-d2` |
| S1 | SQLite + Aorms.Bridge (shared with AQC spike) | **Done** — firm.db · BridgeHost · Activate/Flush · Connect session import |
| S2 | Shell fork: project · estimate · BBS via engine | **Done** — S2a–S2e |
| S3 | Architecture domain modules (fees, drawings, delivery) | **Done** — Focus tabs · local stores · allow-listed meta |
| S4 | Local AI (ESTI) · publish path smoke to hub portal | Not started (meta flush smoke exists) |
| S5 | Signed installer · downloads CTA on studio.aorms.in | Not started (MSIX = D6) |

## S2 checklist

| Slice | Intent | Status |
| --- | --- | --- |
| S2a | HCW geography (Ribbon · Stage · ActionDock) | Done |
| S2b | Tasks local + publish to hub ops | Done |
| S2c | Portfolio: local projects CRUD · Focus selected · `projectStatus` meta | Done |
| S2d | Wire estimate / BBS via in-process `bbs_engine` P/Invoke | Done |
| S2e | Suite handoff to AQC Estimation/BBS from Focus | Done |

## S3 checklist

| Slice | Intent | Status |
| --- | --- | --- |
| S3a | Focus **Brief · Fees · Drawings · Delivery** stage tabs (ribbon stays ≤4 peers) | **Done** |
| S3b | `local_fees` (paise) · publish `invoiceStatus` | **Done** |
| S3c | `local_drawings` register · publish `drawingRegister` | **Done** |
| S3d | `local_delivery` snags/instructions/progress · publish `phaseProgress` | **Done** |
| S3e | Drawing artifact ingest / PDF annotate / deep COA | Later (hub artifact path) |

## D5 — engine pin

See [ENGINE-PIN.md](ENGINE-PIN.md).

```bat
build-engine.cmd
build-winui.cmd
```

## Guardrails

- C++ `bbs_engine` remains SoT for every quantity/money number.
- Sync only allow-listed meta/artifacts ([SYNC-CONTRACT.md](SYNC-CONTRACT.md)).
- No browser staff ERP — no SaaS licence SKUs in this repo.
