# AStudio WinUI shell (D5 + HCW geography)

**Status:** Unpackaged WinUI 3 shell · **Updated:** 2026-08-10 · wave **S4** ✅  
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

| Ribbon | Stage | Dock Save | Dock Publish |
| --- | --- | --- | --- |
| Portfolio | Local projects · Import from Connect | Save project | Publish status |
| Focus | Brief · Fees · Drawings · Delivery | per tab | per tab meta |
| Practice | **Ask ESTI** (Ollama) · hub notes | Probe Ollama | Flush meta |
| Tasks | Local tasks · ops publish | Save local | Publish to hub |

### S4 — Ask ESTI (2026-08-10)

- `EstiOllamaClient` → `GET /api/tags` · `POST /api/chat` on local Ollama.  
- Practice stage: Ask ESTI + Probe; Focus project context injected.  
- Transcripts **never** synced. Env: `ESTI_OLLAMA_URL` · `ESTI_OLLAMA_MODEL`.  
- Ribbon badge shows `Local AI · {model}` when reachable.

### S3 kept

Focus domain tabs · paise fees · drawing register · delivery items.

## Build / run

```bat
build-engine.cmd
build-winui.cmd
set ESTI_HUB_URL=http://127.0.0.1:4000
set ESTI_OLLAMA_URL=http://127.0.0.1:11434
set ESTI_OLLAMA_MODEL=llama3.2
```

firm.db: `%LocalAppData%\AStudio\firm.db` · MSIX = D6.
