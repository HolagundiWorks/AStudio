# Sync contract pin

**Hub API tag:** `2026-08`  
**Bridge doc:** esti `docs/esti/PORTAL-SYNC-BRIDGE.md` (`2026-08-bridge`)  
**Contracts package:** `@esti/contracts` sync enums (publish snapshot into this repo if needed)

## Auth

- `POST /platform/v1/activate` → `licenseToken` + **`syncToken`** (required)  
- Sync calls: `Authorization: Bearer <syncToken>`

## Allow-list

Artifacts: drawing · transmittal · invoice · approval · tender · runningBill ·
inspection · siteVisit · siteReference · progressReport  

Meta: task · taskStatus · estimateTotals · phaseProgress · invoiceStatus ·
drawingRegister · approvalState · projectStatus · presence  

## Never sync

AI transcripts · measurement scratch · nested estimate lines · drafts  

Bump this pin when esti HUB-API / bridge version bumps.
