# AStudio WinUI shell (D5 + HCW geography)

**Status:** Unpackaged WinUI 3 · **Updated:** 2026-08-10 · **S5a web chrome parity** ✅  
**Parity:** esti [`DESKTOP-WEB-PARITY-UX.md`](https://github.com/HolagundiWorks/aorms/blob/main/docs/esti/DESKTOP-WEB-PARITY-UX.md) · [`NAVIGATION.md`](https://github.com/HolagundiWorks/aorms/blob/main/docs/esti/NAVIGATION.md)

## Chrome (match web staff)

```text
┌─ Top ribbon 56px — brand · search · health · Local AI · Ask ESTI ─┐
├─ Stage (Fog) + optional right slot (Ask ESTI) ────────────────────┤
│              ╭─ floating ActionDock (≤5) ─╮                       │
├─ floating Taskbar 60px — Calc|Home | Projects·Clients·People·… ──┤
└─ AnalogueClock 100px BR (clears dock) ────────────────────────────┘
```

| Region | Contents |
| --- | --- |
| **Top ribbon** | Brand → Home · search · office health · Local AI badge · Account stub · Ask ESTI |
| **Stage** | Home · Projects · Project Focus · Clients · Tasks · Stub |
| **ActionDock** | Clear · [Import] · Save · Reload · Publish (orange) — floating above taskbar |
| **Taskbar CENTER** | Projects · Clients · People · Office · Finance · Admin (web `studioNav`) |
| **Taskbar RIGHT** | Tray · Sync · Activate flyout |
| **Clock** | Analogue, fixed BR |

**Module nav is not in the top ribbon** (web law).

### Live vs stub

| Destination | Status |
| --- | --- |
| Home (Studio Intelligence) | Capacity + ESTI probe |
| Projects / Focus (Brief·Fees·Drawings·Delivery) | Live local stores + meta/artifact |
| Clients | Live `local_clients` |
| Tasks | Live bridge tasks |
| People › Work | → Tasks |
| Office › Proposals / Finance › Invoices | → Focus Fees |
| Other Office / People / Finance / Admin / Library | Honest stub stage |

## Build / run

```bat
build-engine.cmd
build-winui.cmd
set ESTI_HUB_URL=http://127.0.0.1:4000
set ESTI_OLLAMA_URL=http://127.0.0.1:11434
```

firm.db: `%LocalAppData%\AStudio\firm.db` · MSIX = D6 (S5b).
