using FreeTrain.Modern;

namespace FreeTrain.Modern.Tests;

public sealed class PluginManifestCatalogTests : IDisposable
{
    private readonly string pluginRoot;

    public PluginManifestCatalogTests()
    {
        pluginRoot = Path.Combine(Path.GetTempPath(), "freetrain-modern-plugin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginRoot);
    }

    [Fact]
    public void CatalogClassifiesStructureAndAccessoryContributionTypes()
    {
        string pluginDirectory = Path.Combine(pluginRoot, "mixed");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllBytes(Path.Combine(pluginDirectory, "sprites.bmp"), new byte[] { 0, 1, 2, 3 });
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <plug-in>
              <title>Mixed plugin</title>
              <contribution type="picture" id="pic">
                <picture src="sprites.bmp"/>
              </contribution>
              <contribution type="GenericStructure" id="gs">
                <group>Generic Group</group>
                <price>1200</price>
                <size>2,3</size>
                <height>4</height>
                <population><base>42</base></population>
                <sprite origin="0,0" offset="80"><picture ref="pic"/></sprite>
              </contribution>
              <contribution type="railStationary" id="rail-static">
                <group>Rail Bits</group>
                <size>1,1</size>
                <height>1</height>
                <sprite origin="0,0" offset="16"><picture ref="pic"/></sprite>
              </contribution>
              <contribution type="roadAccessory" id="road-sign">
                <name>Road Sign</name>
                <sprite><picture ref="pic"/></sprite>
              </contribution>
              <contribution type="electricPole" id="pole">
                <name>Pole</name>
                <sprite><picture ref="pic"/></sprite>
              </contribution>
              <contribution type="varHeightBuilding" id="tower">
                <group>Towers</group>
                <price>9000</price>
                <size>2,2</size>
                <minHeight>3</minHeight>
                <maxHeight>8</maxHeight>
                <pictures>
                  <top origin="0,0" offset="26"><picture ref="pic"/></top>
                  <middle origin="0,50" offset="16"><picture ref="pic"/></middle>
                  <bottom origin="0,90" offset="16"><picture ref="pic"/></bottom>
                </pictures>
              </contribution>
            </plug-in>
            """);

        PluginManifestCatalog catalog = new(pluginRoot, "ja");

        Assert.Single(catalog.Structures, item => item.Id == "gs");
        Assert.Single(catalog.Structures, item => item.Id == "tower");
        Assert.Single(catalog.RailStationaries);
        Assert.Single(catalog.RoadAccessories);
        Assert.Single(catalog.ElectricPoles);

        SpriteContribution generic = catalog.Structures.Single(item => item.Id == "gs");
        Assert.Equal("Generic Group", generic.DisplayName);
        Assert.Equal(1200, generic.Price);
        Assert.Equal(42, generic.PopulationBase);
        Assert.Equal(SpriteContributionPlacementKind.Structure, generic.PlacementKind);

        SpriteContribution variableHeight = catalog.Structures.Single(item => item.Id == "tower");
        Assert.Equal(SpriteContributionPlacementKind.VariableHeightBuilding, variableHeight.PlacementKind);
        Assert.Equal(3, variableHeight.Frames.Count);
        Assert.All(variableHeight.Frames, frame => Assert.True(frame.IsLoadable));
    }

    public void Dispose()
    {
        if (Directory.Exists(pluginRoot))
        {
            Directory.Delete(pluginRoot, recursive: true);
        }
    }
}
