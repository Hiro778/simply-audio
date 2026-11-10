================================================================================
IVORY ISLAND v1.0.0 — FLOATING MEDIA & VOLUME CONTROLLER FOR WINDOWS 10 & 11
================================================================================

SYSTEM REQUIREMENTS
-------------------
* Operating System : Windows 10 x64 (1809 through 22H2 Build 19045) or Windows 11 x64
* Prerequisites    : NONE. (.NET 8 Runtime and Windows App SDK Runtime are 100%
                     self-contained inside the application. You do NOT need to
                     install .NET, Visual Studio, or run any PowerShell commands.)
* Privileges       : Standard User (No Administrator / UAC elevation required)

--------------------------------------------------------------------------------
OPTION 1: STANDARD INSTALLATION (RECOMMENDED) — IvoryIsland-Setup.exe
--------------------------------------------------------------------------------
1. Double-click "IvoryIsland-Setup.exe".
2. In the installation wizard, keep the default per-user directory:
     %LOCALAPPDATA%\Programs\IvoryIsland
   OR click "Browse..." to choose any custom folder or drive, such as:
     D:\Applications\IvoryIsland
3. Choose whether to create a Desktop shortcut and whether to start Ivory Island
   automatically when Windows signs in.
4. Click "Install", then keep "Launch Ivory Island" checked and click "Finish".

--------------------------------------------------------------------------------
OPTION 2: PORTABLE VERSION (NO INSTALLER) — IvoryIsland-Portable\IvoryIsland.exe
--------------------------------------------------------------------------------
1. Copy the "IvoryIsland-Portable" folder to any location or external/secondary
   drive (for example, D:\Tools\IvoryIsland-Portable).
2. Double-click "IvoryIsland.exe" to launch immediately.

--------------------------------------------------------------------------------
USER DATA, SETTINGS & UNINSTALLATION
--------------------------------------------------------------------------------
* All user settings and cached artwork are stored separately from the application
  binaries at:
    %LOCALAPPDATA%\IvoryIsland\settings.json
* To choose a custom storage/cache folder on another drive, click the three-dot
  menu (•••) on the right side of the Ivory Island capsule -> click "Browse..."
  next to "Data Folder:".
* Uninstalling Ivory Island via Windows "Installed Apps" / "Add or Remove Programs"
  removes the application binary and shortcuts cleanly while PRESERVING your
  personal settings and data in %LOCALAPPDATA%\IvoryIsland.

--------------------------------------------------------------------------------
PANDUAN SINGKAT (BAHASA INDONESIA)
--------------------------------------------------------------------------------
1. Tanpa Perlu Install .NET / PowerShell:
   Setelah file Release di-compile melalui GitHub Actions, kamu cukup klik 2x
   "IvoryIsland-Setup.exe" (untuk versi Installer dengan tombol Browse lokasi
   folder) atau langsung klik 2x "IvoryIsland-Portable\IvoryIsland.exe".
2. Cara Build Otomatis Tanpa Install .NET SDK di PC Kamu:
   Karena PC lokal kamu sedang terkendala instalasi .NET SDK, unggah (push)
   folder proyek ini ke repositori GitHub kamu, lalu buka tab "Actions" ->
   pilih "Build Ivory Island Universal Windows 10/11 EXE & Setup" -> klik
   "Run workflow". Server Windows GitHub akan mengompilasi file asli
   "IvoryIsland-Setup.exe" dan "IvoryIsland-Portable\IvoryIsland.exe" yang siap
   diunduh dan dijalankan dengan sekali klik!
================================================================================
