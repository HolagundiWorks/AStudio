# Engine + bridge pin (D5 kickoff)

**Status:** Kickoff · **Updated:** 2026-08-07  
**Upstream:** [HolagundiWorks/AQC](https://github.com/HolagundiWorks/AQC) `main`

## Pin

| Artifact | How to consume |
| --- | --- |
| C++ `bbs_engine` | Submodule or copy from AQC `BBSDesktop/src` — **do not fork the engine** |
| `Aorms.Bridge` | ProjectReference / package from AQC `BBSDesktop/Aorms.Bridge` ([PR #5 merged](https://github.com/HolagundiWorks/AQC/pull/5)) |
| Wire contract | esti [PORTAL-SYNC-BRIDGE](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/PORTAL-SYNC-BRIDGE.md) · HUB-API `2026-08` |

## Next (this repo)

1. Add AQC as git submodule or NuGet/local ProjectReference path.  
2. WinUI shell fork (branding AStudio).  
3. Call `AormsBridgeHost`-style activate/Flush on publish actions.  
4. Local AI (ESTI) — desktop only.

Open source; SaaS licensing deferred.
