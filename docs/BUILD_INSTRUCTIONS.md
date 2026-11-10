# Build & Release Instructions (Zero Local SDK Required)

## Method 1: Automated Cloud Build via GitHub Actions (Recommended When Local .NET SDK Has Issues)

You do **not** need `.NET SDK`, `Visual Studio`, or `Inno Setup` installed on your local computer to produce `IvoryIsland-Setup.exe` and `IvoryIsland-Portable/IvoryIsland.exe`.

1. **Create a GitHub Repository** (public or private) and upload/push the extracted `IvoryIsland` folder to the repository.
2. Open your repository on GitHub and click the **Actions** tab.
3. Select the workflow **"Build Ivory Island Universal Windows 10/11 EXE & Setup"** (`.github/workflows/release.yml`).
4. Click **Run workflow** -> **Run workflow** (or push a tag such as `v1.0.0`).
5. GitHub's `windows-latest` runner automatically:
   - Installs `.NET 8.0 SDK` and verifies `Inno Setup 6` (`ISCC.exe`).
   - Restores NuGet packages for `win-x64`.
   - Runs the `IvoryIsland.Tests` xUnit test suite (`MultiSourceSelectionTests`).
   - Publishes the unpackaged, self-contained, single-file `IvoryIsland.exe` (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, `SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `IncludeAllContentForSelfExtract=true`, `PublishTrimmed=false`).
   - Compiles `installer/IvoryIsland.iss` into `Release/IvoryIsland-Setup.exe`.
   - Generates `Release/Checksums-SHA256.txt` and packages `Release/README-INSTALL.txt`.
6. Once the workflow finishes (~2 minutes), scroll to the **Artifacts** section at the bottom of the workflow run page and download **`IvoryIsland-Windows-x64-Release`**.

## Method 2: Local Windows Build (When .NET 8 SDK & Inno Setup Are Installed)

If you later run on a Windows machine with `.NET 8 SDK` and `Inno Setup 6` installed:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -Configuration Release
```

This populates the `Release/` directory with:
- `Release\IvoryIsland-Setup.exe`
- `Release\IvoryIsland-Portable\IvoryIsland.exe`
- `Release\Checksums-SHA256.txt`
- `Release\README-INSTALL.txt`
