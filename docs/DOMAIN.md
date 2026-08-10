# AStudio domain (architecture practice)

Reference IA from esti [NAVIGATION.md](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/NAVIGATION.md)
— implement natively, do not embed the React SPA.

## Priority modules

| Area | Intent | Desktop status |
| --- | --- | --- |
| Studio Intelligence | Office health · KPIs · attention (local calc + ESTI) | Practice **Ask ESTI** (Ollama); deep KPIs later |
| Projects / brief / R&O | Practice project record | Portfolio + Focus **Brief** |
| Fees · proposals · invoices | India COA/GST money (paise) | Focus **Fees** · `local_fees` · `invoiceStatus` |
| Drawings · transmittals | Issue READY → publish artifact | Focus **Drawings** · `drawingRegister` + S3e `drawing` artifact (sha256) |
| Delivery | Snags · instructions · progress · BBS via engine | Focus **Delivery** · `local_delivery` · `phaseProgress`; BBS = engine smoke / AQC |
| Estimation | Rate books + BOQ via C++ engine | S2d smoke + S2e AQC handoff |
| Team / tasks / ASPRF | Local work; status meta to hub | Tasks module |
| Library | Spec / compliance / standards (local + optional hub) | Not started |

## Focus domain geography (S3)

Ribbon stays **Focus · Portfolio · Practice · Tasks** (≤4). Domain work is
**project-scoped** under Focus:

```text
Focus → Brief | Fees | Drawings | Delivery
```

| Tab | firm.db | Dock Save | Dock Publish (meta) |
| --- | --- | --- | --- |
| Brief | `local_projects` | Save focus | `projectStatus` |
| Fees | `local_fees` (amount_paise) | Save fee | `invoiceStatus` |
| Drawings | `local_drawings` | Save drawing | `drawingRegister` |
| Delivery | `local_delivery` | Save delivery | `phaseProgress` |

## Ask ESTI (S4)

| | |
| --- | --- |
| Runtime | Local Ollama only (`EstiOllamaClient`) |
| Surface | Practice stage — Ask + Probe |
| Context | Focus project brief + fees/drawings/delivery counts |
| Sync | **Never** — transcripts stay on device |

Env: `ESTI_OLLAMA_URL` (default `http://127.0.0.1:11434`) · `ESTI_OLLAMA_MODEL` (default `llama3.2`).

## Out of scope here

- Full PMC contractor ERP (that is **AQC**)  
- Browser staff workspace  
- Cloud / VPS Ollama or hub “Hosted AI”  

