# FreeTrain Modern

This is the side-by-side Avalonia host for migrating FreeTrain EX Av away from
WinForms and DirectX 8 while keeping the original assets, data formats, and game
logic available for incremental porting.

## Build

```sh
dotnet restore FreeTrain.Modern.sln --disable-parallel
dotnet build FreeTrain.Modern.sln
```

## Run

```sh
dotnet run --project src/FreeTrain.Modern/FreeTrain.Modern.csproj
```

The first milestone loads the legacy `core/res` assets, scans `plugin.xml`
manifests, previews plugin picture and sprite contributions, and renders a
simple isometric H/V/Z map preview from `EmptyChip.bmp` with sample plugin
sprites placed onto dry terrain.

Next migration steps are to replace the temporary terrain preview with the real
world model, preserve more of the original sprite sizing metadata, add rail and
station placement tools, then replace WinForms dialogs one workflow at a time.
