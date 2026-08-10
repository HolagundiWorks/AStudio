# Sync contract pin

**Status:** FROZEN after D2 hub smoke · **Updated:** 2026-08-07  
**Hub API tag:** `2026-08`  
**AQC baseline:** tag `aorms-bridge-d2`  
**Bridge doc:** esti `docs/esti/PORTAL-SYNC-BRIDGE.md` (`2026-08-bridge`)  
**Contracts package:** `@esti/contracts` sync enums

Verified: activate → `syncToken` → `POST /api/sync/meta` accepted on colocated hub
via `Aorms.Bridge.Smoke` (`ESTI_LICENSE_KEY` path).

## Auth

- `POST /platform/v1/activate` → `licenseToken` + **`syncToken`** (required)  
- Sync calls: `Authorization: Bearer <syncToken>`

## Endpoints

| Call | Path | Body |
| --- | --- | --- |
| Activate | `POST {LICENSE_API}/v1/activate` | `{ licenseKey, deviceId, deviceName? }` |
| Meta flush | `POST {HUB}/api/sync/meta` | `MetaEventBody` (`stream`, `entity`, `entityId`, `op`, `patch`, `updatedAt`, `conflict`) |
| Artifact | `POST {HUB}/api/sync/ingest` | `SyncIngestBody` (`entity`, `entityId`, `op`, `payload`, `fileKeys`, `contentHash`) |
| Catch-up | `GET {HUB}/api/sync/meta/catch-up` | query `stream`, `afterSeq`, `limit` |

## Allow-list

Artifacts: drawing · transmittal · invoice · approval · tender · runningBill ·
inspection · siteVisit · siteReference · progressReport  

Meta: task · taskStatus · estimateTotals · phaseProgress · invoiceStatus ·
drawingRegister · approvalState · projectStatus · presence  

## Never sync

AI transcripts (incl. Practice Ask ESTI / Ollama) · measurement scratch · nested estimate lines · drafts  

## AStudio S3 usage

| Focus tab | Enqueued entity | Notes |
| --- | --- | --- |
| Brief | `projectStatus` | Portfolio / Focus |
| Fees | `invoiceStatus` | `amountPaise` in patch |
| Drawings | `drawingRegister` | Meta only; artifact ingest later |
| Delivery | `phaseProgress` | Snag / instruction / progress kinds |

Bump this pin when esti HUB-API / bridge version bumps.
