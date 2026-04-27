# FreeTrain Modern

This is the side-by-side Avalonia host for migrating FreeTrain EX Av away from
WinForms and DirectX 8 while keeping the original assets, data formats, and game
logic available for incremental porting.

## Build

```sh
dotnet restore FreeTrain.Modern.sln --disable-parallel
dotnet build FreeTrain.Modern.sln
dotnet test FreeTrain.Modern.sln
```

## Run

```sh
dotnet run --project src/FreeTrain.Modern/FreeTrain.Modern.csproj
```

The modern solution is split into a portable core library and an Avalonia host.
The core loads legacy resources, scans `plugin.xml` manifests, applies plugin
translation sidecars, owns world state, snapshots, terrain, accounting, and
transport simulation. The Avalonia host renders the isometric map and plugin
previews from the original bitmap assets.

The app now starts from a real new-world creation path instead of the old rail
loop sample. `File > New World...` creates an empty player-built world from
modern creation options. Next migration steps are to expand the new-game dialog
into full scenario/options support, preserve more of the original sprite sizing
metadata, deepen rail and station behavior, then replace WinForms dialogs one
workflow at a time.
