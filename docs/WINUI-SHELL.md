# AStudio WinUI shell (D5)

**Status:** Unpackaged WinUI 3 shell builds on VS 2022 Community · **Updated:** 2026-08-07

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

MSIX signing / Store package = D6. Domain UI (fork of AQC BBSApp) = next.
