# AStudio WinUI shell (D5 + HCW geography)

**Status:** Unpackaged WinUI 3 shell · **Updated:** 2026-08-10 · wave **S3** ✅  
**Parity:** esti [`DESKTOP-WEB-PARITY-UX.md`](https://github.com/HolagundiWorks/aorms/blob/main/docs/esti/DESKTOP-WEB-PARITY-UX.md)

## Chrome (HCW scaffold)

```text
┌─ Ribbon (Focus · Portfolio · Practice · Tasks) ─────────────┐
├─ Stage (Fog Gray) — module panels ──────────────────────────┤
├─ ActionDock — Clear · [Import] · Save · Reload · Publish ───┤
└─ Status tray ───────────────────────────────────────────────┘
```

- Canvas `#F2F4F7` · soft chrome `#ECEEF2` · ink `#141517` · accent `#FF4F18` · **8px** radius  
- Dock zones: destroy LEFT · create CENTRE · commit RIGHT  
- Dock ≤5 actions; **one** orange commit  
- Local AI badge on ribbon — ESTI does **not** run on the hub  

| Ribbon | Stage |
| --- | --- |
| Portfolio | Local projects CRUD · Import from Connect |
| Focus | **Brief · Fees · Drawings · Delivery** (S3) · engine smoke · AQC handoff |
| Practice | Hub Activate / Flush |
| Tasks | Local tasks · ops publish |

### S3 — domain modules (2026-08-10)

Project-scoped under Focus (ribbon capacity preserved):

| Tab | Store | Meta on Publish |
| --- | --- | --- |
| Fees | `local_fees` (paise) | `invoiceStatus` |
| Drawings | `local_drawings` | `drawingRegister` |
| Delivery | `local_delivery` | `phaseProgress` |

Drawing PDF artifact ingest and deep COA = later (S3e).

### S2 kept

Engine smoke (S2d) · Open AQC Estimation/BBS (S2e) · Connect import auto-Focus.

## Build

```bat
build-engine.cmd
build-winui.cmd
```

## Run

```bat
set ESTI_HUB_URL=http://127.0.0.1:4000
src\AStudio.App\bin\x64\Release\net8.0-windows10.0.19041.0\AStudio.exe
```

firm.db: `%LocalAppData%\AStudio\firm.db`  
MSIX = D6.
