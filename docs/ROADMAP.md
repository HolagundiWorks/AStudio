# AStudio desktop roadmap

**Current wave:** S2 — Projects / Portfolio shell  
**Updated:** 2026-08-10  
Upstream tracker: esti [ROADMAP.md](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/ROADMAP.md) D-waves.  
Open source until SaaS licensing is decided.

## Waves

| Wave | Outcome | Status |
| --- | --- | --- |
| S0 | Repo scaffold + pin AQC engine commit | **Done** — `vendor/AQC` @ `aorms-bridge-d2` |
| S1 | SQLite + Aorms.Bridge (shared with AQC spike) | **Done** — firm.db · BridgeHost · Activate/Flush · Connect session import |
| S2 | Shell fork: project · estimate · BBS via engine | **In progress** — S2a–S2e ✅ (smoke); full sheets = S3 |
| S3 | Architecture domain modules (fees, drawings, delivery) | Not started |
| S4 | Local AI (ESTI) · publish path smoke to hub portal | Not started (meta flush smoke exists) |
| S5 | Signed installer · downloads CTA on studio.aorms.in | Not started (MSIX = D6) |

## S2 checklist

| Slice | Intent | Status |
| --- | --- | --- |
| S2a | HCW geography (Ribbon · Stage · ActionDock) | Done |
| S2b | Tasks local + publish to hub ops | Done |
| S2c | Portfolio: local projects CRUD · Focus selected · `projectStatus` meta | **Done** · Focus empty-state · Connect import · dock ≤5 |
| S2d | Wire estimate / BBS via in-process `bbs_engine` P/Invoke | **Done** · `BbsEngineClient` · Focus **Engine smoke (column)** · `build-engine.cmd` |
| S2e | Suite handoff to AQC Estimation/BBS from Focus | **Done** · Open AQC Estimation / BBS buttons (env + install paths) |

## D5 — engine pin

See [ENGINE-PIN.md](ENGINE-PIN.md). Aorms.Bridge landed on AQC main (PR #5).

```bat
build-engine.cmd
build-winui.cmd
```

`bbs_engine.dll` copies beside `AStudio.exe` when present under `vendor/AQC/BBSDesktop/build/`.

## Guardrails

- C++ `bbs_engine` remains SoT for every quantity/money number.
- Sync only allow-listed meta/artifacts ([SYNC-CONTRACT.md](SYNC-CONTRACT.md)).
- No browser staff ERP — no SaaS licence SKUs in this repo.
