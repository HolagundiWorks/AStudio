# AStudio WinUI shell (D5 + HCW geography)

**Status:** Unpackaged WinUI 3 · **Updated:** 2026-08-10 · **S5a** ✅ · **S6 Focus depth** ✅ · **HCW visual polish** ✅  
**Parity:** esti [`DESKTOP-WEB-PARITY-UX.md`](https://github.com/HolagundiWorks/aorms/blob/main/docs/esti/DESKTOP-WEB-PARITY-UX.md) · [`PAGE-STRUCTURE.md`](https://github.com/HolagundiWorks/aorms/blob/main/docs/esti/PAGE-STRUCTURE.md) · [`NAVIGATION.md`](https://github.com/HolagundiWorks/aorms/blob/main/docs/esti/NAVIGATION.md)

## Chrome (match web staff)

```text
┌─ Top ribbon 56px — brand · search · health · Local AI · Ask ESTI ─┐
├─ Stage (Fog) + optional right slot (Ask ESTI) ────────────────────┤
│              ╭─ floating ActionDock (≤5) ─╮                       │
├─ floating Taskbar 60px — Calc|Home | Projects·Clients·People·… ──┤
└─ AnalogueClock 0.8× (face 80) BR (clears dock) ───────────────────┘
```

| Region | Contents |
| --- | --- |
| **Top ribbon** | AORMS mark + AStudio · search · office health · Local AI badge · Account · Ask ESTI |
| **Stage** | Home · Projects · Project Focus · Clients · Tasks · Stub |
| **ActionDock** | Kit peer: hug-content soft tray · LEFT Clear (danger) · CENTER Save · RIGHT Reload · Publish (accent text) · vertical grooves |
| **Taskbar CENTER** | Projects · Clients · People · Office · Finance · Admin (web `studioNav`) |
| **Taskbar RIGHT** | Tray · Sync · Licence flyout (Connect session — no HLP Activate) |
| **Clock** | Analogue, fixed BR |

**Module nav is not in the top ribbon** (web law).

### HCW visual contract (WinUI mirror)

Tokens live in `Themes/HcwTheme.xaml` (mirror of kit — not a second design system):

| Token | Value | Role |
| --- | --- | --- |
| Fog / Soft / White | `#F2F4F7` / `#ECEEF2` / `#FFFFFF` | Flat stage · soft chrome · cards |
| Ink / Accent | `#141517` / `#FF4F18` | Body · scarce CTA / active peers |
| Radius / hits | **8px** · dock **44** · taskbar **35** | Constitution |
| Geography | ribbon 56 · footer 60 · inset **24** stage gutter · dock bottom **92** · clock **80** (0.8× of 100) | staff stage **full width** (not portal 1200 column) |
| Stage width | Stretch under ribbon · gutters 24 · bottom pad for dock/taskbar | Portal/marketing keep `contentMaxPx` 1200; AStudio staff does not |
| Icons | Segoe Fluent Icons (`FontIcon`) | Ribbon · taskbar · dock |
| Brand | `Assets/aorms-mark.png` via `BitmapIcon` (Radiant Orange tint) · `favicon.ico` window icon | Same mark as web mask |
| Calculator | Taskbar Calc → floating flyout (`OfficeCalculator`) | Bare numbers = metres · m / ft·in |
| Wellness | Taskbar LEFT → **in-window** soft panel (320×340, 1× shell — not system Flyout) | `WellnessSession` + `WellnessPrefs` + banner · web `WellnessPanel` peer |
| Clock | Analogue + Pomodoro ring/crown/label (web `MarketingClockPomodoro` peer) | Click start/pause · drag crown · double-click reset |
| Elevation | Dual-offset soft-neu (`NEU_RAISED` peer: dark SE + light NW on `#ECEEF2`) — **not** ThemeShadow | Soft raised · **no glass/blur** |
| Ribbon | Floating inset Soft Surface (Margin 16,16,16,8) — not flush edge bar | Web `AppRibbon` peer |
| Active nav | Transparent + **2px accent underline** (taskbar · Focus tabs) | Web `navSx` — orange fill only on Publish |
| Home | Soft brief + one KPI strip (hairline columns) + Attention list | Web Studio Intelligence anatomy |
| Taskbar L/C/R | Wellness · Calc \| Home + studioNav \| Sync chip · Activate | Web `AppFooterBar` |
| Heights | Ribbon **56** · dock/taskbar trays **60** · dock hits **44** · taskbar hits **35** | `PORTAL_CHROME` |
| UI density | **1×** — no window scale transform | Matches web `PORTAL_CHROME` (56 · 60 · 44 · 35); clock dial **0.8×** (80) |
| Type | Segoe UI Variable (system peer to Urbanist until OFL pack) | Brand-adjacent |

### Live vs stub

| Destination | Status |
| --- | --- |
| Home (Studio Intelligence) | ≤4 KPIs · attention list · ESTI probe · hub demo sync |
| Projects | ListView (ref · title · status · phase · publish) · filter · Open Focus |
| Focus | **Overview · Brief · Drawings · Documents · Fees · Site** — ListView + forms + Flush |
| Clients | ListView + form (`local_clients`) |
| Tasks | ListView + status toggle · Bridge publish |
| People › Work | → Tasks |
| Office › Proposals / Finance › Invoices | → Focus Fees |
| Other Office / People / Finance / Admin / Library | Honest stub stage |

## Build / run

```bat
build-engine.cmd
build-winui.cmd
```

**Local Docker hub bind** (firm.db stays SQLite; hub = Postgres/Mongo in compose):

```bat
REM esti repo: docker compose up -d
REM            docker compose exec backend pnpm --filter @esti/backend seed:desktop-local-hub
run-local-hub.cmd
```

See [LOCAL-HUB.md](LOCAL-HUB.md). Manual env: `ESTI_HUB_URL` · `ESTI_LICENSE_API_URL` ·
`ESTI_PRODUCT_API_KEY` · `ESTI_OLLAMA_URL`.

firm.db: `%LocalAppData%\AStudio\firm.db` · MSIX = D6 (S5b).
