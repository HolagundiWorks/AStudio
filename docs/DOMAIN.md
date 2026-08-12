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
| Studio Intelligence | Office health · KPIs · attention | **Home** ≤4 KPIs + attention + Ask ESTI |
| Projects list | Practice portfolio | **Projects** ListView (ref · title · status · phase · publish) |
| Project Focus | Web ProjectDetail peer (thin) | **S6** six tabs (below) |
| Fees · proposals · invoices | India COA/GST money (paise) | Focus **Fees** · Finance › Invoices |
| Drawings · transmittals | Issue READY → publish artifact | Focus **Drawings** + S3e ingest |
| Site supervision | Visits · snags · progress | Focus **Site** (facets) |
| Estimation | Rate books + BOQ via C++ engine | Engine smoke + AQC handoff |
| Team / tasks | Local work; status meta to hub | Tasks · People › Work |
| Clients | Client directory | **Clients** ListView + form |
| Library / Office papers / HR | Web depth | Stub stages |

## Focus tabs (S6)

| Tab | Local store | Publish |
| --- | --- | --- |
| **Overview** | `local_decisions` · `local_critical_notes` | `approvalState` · `presence` |
| **Brief** | `local_projects` (+ client/jurisdiction/site/work_type) · `local_risks` | `projectStatus` |
| **Drawings** | `local_drawings` | `drawingRegister` + artifact |
| **Documents** | `local_documents` | `presence` (`documentRegister`) |
| **Fees** | `local_fees` | `invoiceStatus` |
| **Site** | `local_delivery` (VISIT / SNAG / PROGRESS) | `phaseProgress` |

Handoffs: Open AQC Estimation / BBS from Brief. **No** in-app BOQ, moodboard canvas, or tenders in S6.

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
- Office Leads/Tenders/Contracts · Finance reconcile/payroll · Library (honest stubs)  
