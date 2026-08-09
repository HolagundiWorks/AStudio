# AStudio WinUI shell (D5 + HCW geography)

**Status:** Unpackaged WinUI 3 shell · **Updated:** 2026-08-09 · wave **S2c** ✅  
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
| Focus | Selected project brief · empty-state when none · Save focus · `projectStatus` publish |
| Practice | Hub Activate / Flush |
| Tasks | Local tasks · ops publish |

### S2c polish (2026-08-09)

- **Focus empty state** — clear copy + “Go to Portfolio”; Save focus / Publish status disabled until a project is selected.  
- **Focus publish** — Save focus runs before Publish status; stays on Focus (no yank to Practice on flush skip).  
- **Connect import** — prominent “Import from Connect” on Portfolio stage + dock; `CatalogImportNote` reports new vs skipped; **auto-opens Focus** after first successful import when Focus was empty.  
- **Next:** S2d = estimate/BBS via `bbs_engine` (not started here). Fees · drawings · delivery = S3.

## Build

Use **Visual Studio 2022 MSBuild** (not `dotnet build` alone — SDK 10 misses Appx Pri tasks):

```bat
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

Activate with a live key → Enqueue smoke meta → Flush.

## Pin

- Submodule: `vendor/AQC` @ tag `aorms-bridge-d2`
- Bridge: `Aorms.Bridge` via ProjectReference
- firm.db: `%LocalAppData%\AStudio\firm.db`

MSIX signing / Store package = D6. Full domain UI (fork of AQC BBSApp) = next.
