# AStudio WinUI shell (D5 + HCW geography)

**Status:** Unpackaged WinUI 3 shell · **Updated:** 2026-08-10 · wave **S2d** ✅  
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
- Dock ≤5 actions; **one** orange commit (`Publish status` / `Publish to hub` / `Flush meta`)  
- Local AI badge on ribbon — ESTI does **not** run on the hub  

| Ribbon | Stage |
| --- | --- |
| Portfolio | Local projects CRUD · **Import from Connect** (stage card + dock) · import status note |
| Focus | Selected project brief · **Engine smoke (column)** · Open AQC Estimation/BBS · `projectStatus` publish |
| Practice | Hub Activate / Flush |
| Tasks | Local tasks · ops publish |

### S2d — in-process engine (2026-08-10)

- `BbsEngineClient` P/Invoke → `bbs_engine.dll` (AQC ABI).  
- Focus **Engine smoke (column)** runs a sample rectangular column and shows BBS/summary counts.  
- Build DLL: `build-engine.cmd` (MSVC) → `vendor/AQC/BBSDesktop/build/bbs_engine.dll` (gitignored).  
- csproj copies the DLL beside `AStudio.exe` when present.  
- Full BBS sheets / estimate UI remain in AQC (S2e handoff); S3 adds domain depth in AStudio.

### S2c polish (kept)

- Focus empty state · publish saves brief first · Connect import auto-opens Focus when empty.

## Build

```bat
build-engine.cmd
build-winui.cmd
```

Or open `src\AStudio.App\AStudio.App.csproj` in VS and F5 (x64).

## Run

```bat
set ESTI_HUB_URL=http://127.0.0.1:4000
set ESTI_LICENSE_API_URL=http://127.0.0.1:4000/platform
set ESTI_PRODUCT_API_KEY=hlp_sk_...
src\AStudio.App\bin\x64\Release\net8.0-windows10.0.19041.0\AStudio.exe
```

## Pin

- Submodule: `vendor/AQC`  
- Bridge: `Aorms.Bridge` via ProjectReference  
- firm.db: `%LocalAppData%\AStudio\firm.db`

MSIX signing / Store package = D6.
