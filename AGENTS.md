# AStudio — agent contract

**Product:** AStudio (Accelerated Studio) — architecture practice desktop OS.  
**Stack:** C# WinUI 3 + C++ `bbs_engine` (fork AQC). **Not** the esti React SPA.

## Do

- Keep **C++ engine** as SoT for every quantity/money number.  
- Persist firm work in **local SQLite**; push only allow-listed meta/artifacts via `aorms_bridge`.  
- Follow [docs/SYNC-CONTRACT.md](docs/SYNC-CONTRACT.md) + esti PORTAL-SYNC-BRIDGE.  
- Local AI only (Ollama / opt-in keys). Never require hub AI for staff work.  
- Stay **open source**; do not invent SaaS licence SKUs here.

## Do not

- Reimplement BBS/estimate in TypeScript or C#.  
- Sync drafts, AI chats, or unissued drawings.  
- Build browser staff ERP in this repo.  
- Diverge `bbs_engine` from the shared AQC pin without an explicit engine PR.

## Read first

1. [README.md](README.md)  
2. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)  
3. [docs/DOMAIN.md](docs/DOMAIN.md)  
4. [docs/PORTAL-PUSH.md](docs/PORTAL-PUSH.md)  
5. [docs/ROADMAP.md](docs/ROADMAP.md)  
