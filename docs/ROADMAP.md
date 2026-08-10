# AStudio desktop roadmap

**Current wave:** S5a — Web chrome parity  
**Updated:** 2026-08-10  
Upstream tracker: esti [ROADMAP.md](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/ROADMAP.md) D-waves.  
Open source until SaaS licensing is decided.

## Waves

| Wave | Outcome | Status |
| --- | --- | --- |
| S0 | Repo scaffold + pin AQC engine commit | **Done** — `vendor/AQC` @ `aorms-bridge-d2` |
| S1 | SQLite + Aorms.Bridge (shared with AQC spike) | **Done** — firm.db · BridgeHost · Activate/Flush · Connect session import |
| S2 | Shell fork: project · estimate · BBS via engine | **Done** — S2a–S2e |
| S3 | Architecture domain modules (fees, drawings, delivery) | **Done** — S3a–S3e (artifact ingest envelope) |
| S4 | Local AI (ESTI) · publish path smoke to hub portal | **Done** |
| S5a | Web chrome parity (ribbon · dock · taskbar · studioNav) | **Done** |
| S5b | Signed installer · downloads CTA | Not started (MSIX = D6) |

## S3 checklist

| Slice | Intent | Status |
| --- | --- | --- |
| S3a–S3d | Focus Brief · Fees · Drawings · Delivery | Done |
| S3e | Drawing artifact ingest (sha256 · Bridge `/api/sync/ingest`) | **Done** · binary upload later |

## S4 checklist

| Slice | Intent | Status |
| --- | --- | --- |
| S4a | `EstiOllamaClient` → local Ollama (`ESTI_OLLAMA_URL` / `ESTI_OLLAMA_MODEL`) | **Done** |
| S4b | Practice **Ask ESTI** panel + Probe · mission-style system prompt | **Done** |
| S4c | Focus project context in prompt; transcripts never synced | **Done** |
| S4d | Hub publish path | Already via Practice Flush / Focus Publish (meta allow-list) |

```bat
REM optional — defaults shown
set ESTI_OLLAMA_URL=http://127.0.0.1:11434
set ESTI_OLLAMA_MODEL=llama3.2
ollama pull llama3.2
```

## D5 — engine pin

See [ENGINE-PIN.md](ENGINE-PIN.md).

```bat
build-engine.cmd
build-winui.cmd
```

## Guardrails

- C++ `bbs_engine` remains SoT for every quantity/money number.
- ESTI / Ollama is **desktop only** — never the cloud hub or aorms.in VPS.
- Sync only allow-listed meta/artifacts ([SYNC-CONTRACT.md](SYNC-CONTRACT.md)); **no AI transcripts**.
- No browser staff ERP — no SaaS licence SKUs in this repo.
