# Engine + bridge pin (D5)

**Status:** Pin ready Â· **Updated:** 2026-08-07  
**Upstream:** [HolagundiWorks/AQC](https://github.com/HolagundiWorks/AQC)  
**Baseline:** tag `aorms-bridge-d2` Â· commit `1184390`

## Pin

| Artifact | How to consume |
| --- | --- |
| C++ `bbs_engine` | Submodule or copy from AQC `BBSDesktop/src` â€” **do not fork the engine** |
| `Aorms.Bridge` | ProjectReference to AQC `BBSDesktop/Aorms.Bridge` at tag `aorms-bridge-d2` |
| Wire contract | esti [PORTAL-SYNC-BRIDGE](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/PORTAL-SYNC-BRIDGE.md) Â· HUB-API `2026-08` |

## Submodule (recommended)

```bash
git submodule add https://github.com/HolagundiWorks/AQC.git vendor/AQC
cd vendor/AQC && git checkout aorms-bridge-d2
```

Then in the WinUI `.csproj`:

```xml
<ProjectReference Include="..\..\vendor\AQC\BBSDesktop\Aorms.Bridge\Aorms.Bridge.csproj" />
```

## Next (this repo)

1. Done: submodule at `aorms-bridge-d2`.  
2. Done: WinUI shell + activate/Flush.  
3. Now: S2c Portfolio/Focus — see [ROADMAP.md](ROADMAP.md).  
4. Next: S2d P/Invoke `bbs_engine`; S4 local AI (ESTI) desktop only.

Open source; SaaS licensing deferred.
