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

    [Fact]
    public void CatalogParsesNonBgmRuntimeContributionTypes()
    {
        string pluginDirectory = Path.Combine(pluginRoot, "runtime");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllBytes(Path.Combine(pluginDirectory, "sprites.bmp"), new byte[] { 0, 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(pluginDirectory, "bell.wav"), new byte[] { 4, 5, 6, 7 });
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.xml"), """
            <?xml version="1.0" encoding="utf-8"?>
            <plug-in>
              <title>Runtime plugin</title>
              <contribution type="picture" id="pic">
                <picture src="sprites.bmp"/>
              </contribution>
              <contribution type="contribution" id="factory">
                <name>customThing</name>
                <class name="Factory" codebase="factory.dll"/>
                <implementation name="Thing" codebase="thing.dll"/>
              </contribution>
              <contribution type="menu" id="menu">
                <name>Menu Hook</name>
                <class name="MenuImpl" codebase="menu.dll"/>
              </contribution>
              <contribution type="dockingContent" id="dock">
                <name>Dock Window</name>
                <multiple/>
                <menu name="Dock" location="view" position="2"/>
                <class name="DockImpl"/>
              </contribution>
              <contribution type="accountGenre" id="genre">
                <name>Railway</name>
              </contribution>
              <contribution type="specialRail" id="bridge">
                <class name="BridgeRail"/>
              </contribution>
              <contribution type="specialStructure" id="stadium">
                <name>Stadium</name>
                <description>Large venue</description>
                <class name="StadiumImpl"/>
              </contribution>
              <contribution type="trainController" id="tc">
                <name>Manual</name>
                <description>Manual control</description>
                <class name="ManualController"/>
              </contribution>
              <contribution type="newGame" id="new">
                <name>Empty Map</name>
                <author>Core</author>
                <description>Starts empty</description>
                <class name="EmptyNewGame"/>
              </contribution>
              <contribution type="spriteFactory" id="sf">
                <class name="SpriteFactory"/>
              </contribution>
              <contribution type="spriteLoader" id="sl">
                <class name="SpriteLoader"/>
              </contribution>
              <contribution type="ColorLibrary" id="colors">
                <name>Palette</name>
                <element color="1,2,3"/>
              </contribution>
              <contribution type="colorMapTrainPicture" id="trainpic">
                <name>Uncolored</name>
                <author>Painter</author>
                <picture ref="pic"/>
              </contribution>
              <contribution type="trainDepartureBell" id="bell">
                <name>Bell</name>
                <sound href="bell.wav"/>
              </contribution>
              <contribution type="railSignal" id="signal">
                <name>Signal</name>
                <side>left</side>
                <picture ref="pic"/>
              </contribution>
              <contribution type="DummyCar" id="bus">
                <name>Bus</name>
                <sprite origin="0,0" offset="8">
                  <picture ref="pic"/>
                  <variations>
                    <colorVariation><map from="1,2,3" to="4,5,6"/></colorVariation>
                  </variations>
                </sprite>
              </contribution>
              <contribution type="HalfVoxelStructure" id="hv">
                <group>Houses</group>
                <subgroup>Small</subgroup>
                <name>Small House</name>
                <price>4500</price>
                <height>1</height>
                <population><base>3</base></population>
                <sprite>
                  <picture ref="pic"/>
                  <map from="0,*,0" to="colors"/>
                  <pattern direction="west" side="either" origin="0,0"/>
                  <pattern direction="south" side="back" origin="24,0"/>
                </sprite>
              </contribution>
            </plug-in>
            """);

        PluginManifestCatalog catalog = new(pluginRoot, "ja");

        Assert.Single(catalog.ContributionFactories);
        Assert.Single(catalog.Menus);
        Assert.Single(catalog.DockingContents);
        Assert.Single(catalog.AccountGenres);
        Assert.Single(catalog.SpecialRails);
        Assert.Single(catalog.SpecialStructures);
        Assert.Single(catalog.TrainControllers);
        Assert.Single(catalog.NewGames);
        Assert.Single(catalog.SpriteFactories);
        Assert.Single(catalog.SpriteLoaders);
        Assert.Single(catalog.ColorLibraries);
        Assert.Single(catalog.ColorMapTrainPictures);
        Assert.Single(catalog.TrainDepartureBells);
        Assert.Single(catalog.RailSignals);
        Assert.Single(catalog.DummyCars);
        Assert.Single(catalog.HalfVoxelStructures);

        Assert.True(catalog.ColorMapTrainPictures[0].IsLoadable);
        Assert.True(catalog.TrainDepartureBells[0].IsLoadable);
        Assert.True(catalog.RailSignals[0].IsLoadable);
        Assert.True(catalog.DummyCars[0].IsLoadable);
        Assert.True(catalog.HalfVoxelStructures[0].IsLoadable);
        Assert.Single(catalog.RailStationaries, item => item.Id == "signal");
        Assert.Single(catalog.RoadAccessories, item => item.Id == "bus");
        Assert.Single(catalog.Structures, item => item.Id == "hv");
        Assert.Equal(SpriteContributionPlacementKind.RailStationary, catalog.Sprites.Single(item => item.Id == "signal").PlacementKind);
        Assert.Equal(SpriteContributionPlacementKind.RoadAccessory, catalog.Sprites.Single(item => item.Id == "bus").PlacementKind);
        Assert.Equal("Dock", catalog.DockingContents[0].MenuName);
        Assert.Equal(2, catalog.DockingContents[0].MenuPosition);
        Assert.Equal("BridgeRail", catalog.SpecialRails[0].Class.Name);
        Assert.Equal(ModernSpecialRailKind.Unsupported, catalog.SpecialRails[0].Kind);
        Assert.Equal("colors", catalog.HalfVoxelStructures[0].ColorLibraryId);
        Assert.Equal(3, catalog.HalfVoxelStructures[0].PopulationBase);
    }

    public void Dispose()
    {
        if (Directory.Exists(pluginRoot))
        {
            Directory.Delete(pluginRoot, recursive: true);
        }
    }
}
