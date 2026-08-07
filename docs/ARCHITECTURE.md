# AStudio architecture

```
WinUI 3 shell (C#)
  ├── Domain UI (projects, fees, drawings, delivery, office…)
  ├── Local SQLite firm DB
  ├── Aorms.Bridge (licence · meta outbox · artifact outbox)
  └── P/Invoke → bbs_engine.dll (C++ JSON C API)
         └── BBS · BOQ · estimate rollups (SoT)
```

| Layer | Tech |
| --- | --- |
| UI | C# WinUI 3 Fluent 2 |
| Engine | C++ `bbs_engine` from AQC pin |
| Local DB | SQLite |
| Sync | `aorms_bridge` → AORMS hub ([PORTAL-SYNC-BRIDGE](https://github.com/HolagundiWorks/esti/blob/main/docs/esti/PORTAL-SYNC-BRIDGE.md)) |
| Reports | QuestPDF or shared AQC reporters |

Hub never recomputes engine numbers. Portals read published payloads only.
