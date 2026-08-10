# AStudio domain (architecture practice)

Reference IA from esti [NAVIGATION.md](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/NAVIGATION.md)
— implement natively, do not embed the React SPA.

## Chrome geography (S5a)

| Region | Peers |
| --- | --- |
| Top ribbon | Brand · search · health · Local AI · Ask ESTI — **not** module nav |
| Taskbar CENTER | **Projects · Clients · People · Office · Finance · Admin** (web studioNav) |
| Right slot | Ask ESTI (Ollama) |
| Stage | Home · Projects · Focus · Clients · Tasks · stubs |

## Priority modules

| Area | Intent | Desktop status |
| --- | --- | --- |
| Studio Intelligence | Office health · KPIs · attention | **Home** capacity + Ask ESTI slot |
| Projects / brief / R&O | Practice project record | Projects + Focus **Brief** |
| Fees · proposals · invoices | India COA/GST money (paise) | Focus **Fees** · Finance › Invoices |
| Drawings · transmittals | Issue READY → publish artifact | Focus **Drawings** + S3e ingest |
| Delivery | Snags · instructions · progress | Focus **Delivery** |
| Estimation | Rate books + BOQ via C++ engine | Engine smoke + AQC handoff |
| Team / tasks | Local work; status meta to hub | Tasks · People › Work |
| Clients | Client directory | **Clients** `local_clients` |
| Library / Office papers / HR | Web depth | Stub stages |

## Ask ESTI (S4)

| | |
| --- | --- |
| Runtime | Local Ollama only (`EstiOllamaClient`) |
| Surface | Right slot + Home probe |
| Sync | **Never** |

## Out of scope here

- Full PMC contractor ERP (that is **AQC**)  
- Browser staff workspace  
- Cloud / VPS Ollama  
