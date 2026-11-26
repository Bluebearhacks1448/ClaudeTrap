# ClaudeTrap

ClaudeTrap is an independent Windows Roblox bootstrapper fork based on [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap). It preserves the upstream launcher foundation while developing its own branding and features.

<p align="center">
  <img src="Images/ClaudeTrap.svg" width="132" alt="ClaudeTrap logo">
</p>

## Project status

ClaudeTrap is an early-stage fork. The current codebase is inherited from Bloxstrap; project-specific work is being developed on the `claudetrap` branch. Until a ClaudeTrap release is published, use GitHub Actions artifacts for test builds rather than assuming there is a stable installer.

Every push to `claudetrap` runs Windows Debug and Release builds. Download test builds from the repository's Actions artifacts.

## Inherited functionality

- Roblox installation, update and launch handling
- Customizable bootstrapper appearance and Roblox shortcut icons
- Fast Flag configuration
- Local content mods and custom bootstrapper themes
- Discord Rich Presence and server information

ClaudeTrap is intended for Windows PCs. It is not an exploit and does not modify Roblox game code. No launcher can guarantee how Roblox will treat every configuration or third-party modification. The current build keeps diagnostic logs locally and does not upload usage analytics or crash logs.

## Claude desktop pet

Open **ClaudeTrap → ClaudeTrap** in Settings, enable the desktop pet, choose **2D cartoon** or **3D toy**, adjust its speed, then select **Launch Claude**.

The pet runs in its own process, so it remains on the desktop after the settings window closes. **Left-click and drag** Claude to pick it up; release while moving to throw it. It wanders along the desktop, jumps to nearby application-window edges and climbs taller windows. **Right-click** Claude to pause/resume, switch between 2D and 3D, or disable it. The pet starts near the lower-right edge each time.

## Build from source

Requirements: Windows, the .NET 10 SDK (10.0.100 or a later servicing update in the same feature band), and Git with submodule support.

The published Windows executable is built as a self-contained single-file app and includes the .NET 10 runtime; end users do not need to install the .NET Desktop Runtime separately.

```powershell
git clone --recurse-submodules https://github.com/Bluebearhacks1448/ClaudeTrap.git
cd ClaudeTrap
git switch claudetrap
dotnet restore .\Bloxstrap.sln
dotnet build .\Bloxstrap.sln -c Release
```

If you cloned without submodules, run:

```powershell
git submodule update --init --recursive
```

## Upstream attribution and license

ClaudeTrap is derived from [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap), originally created by pizzaboxer and maintained by its contributors. The original MIT license and copyright notice are retained in [LICENSE](LICENSE). ClaudeTrap changes are maintained separately in this repository.

ClaudeTrap is an independent community project and is not affiliated with or endorsed by Roblox Corporation or Bloxstrap Labs. UI translations retain upstream contributor credit while displaying the ClaudeTrap product name.
