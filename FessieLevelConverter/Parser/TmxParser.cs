using System.IO;
using System.Windows;
using System.Xml;
using FessieLevelConverter.Tmx;
using static FessieLevelConverter.Fessie.WallIDs;
using static FessieLevelConverter.Tmx.TileType;

namespace FessieLevelConverter.Parser;

using System.Xml;
using System.Xml.Linq;
using FessieLevelConverter.Fessie;


public static class TmxParser
{
    
    public static readonly string TMX_GID_EMPTY = "0";

    /// <summary>
    /// return properties and nested property tag as dictionary.
    /// </summary>
    private static Dictionary<string, string> GetProperties(XElement element)
    {
        var properties = element.Element("properties")?.Elements("property");
        if (properties == null)
        {
            return new Dictionary<string, string>();
        }

        return properties
            .Where(prop => (prop.Attribute("name") is not null) && prop.Attribute("value") is not null)
            .Select(x => new KeyValuePair<string, string>(x.Attribute("name")!.Value, x.Attribute("value")!.Value))
            .ToDictionary();
    }
    
    /// <summary>
    /// return tiles as mapping of local gids -> ids. Optionally you can revert the mapping.
    /// </summary>
    private static Dictionary<string, Tile> LoadExternalTileset(string externalSourceFilepath, int firstTilesetGid, out int countTiles)
    {
        
        var tileset = XElement.Load(externalSourceFilepath).Elements("tile");
        var mapping = new Dictionary<string, Tile>();
        countTiles = tileset.Count();
        foreach (var tile in tileset)
        {
            var localGid = tile.Attribute("id")!.Value;
            var globalGid = (firstTilesetGid + Int32.Parse(localGid)).ToString();
            var properties = GetProperties(tile);
            properties.TryGetValue("FessieId", out var fessieId);
            properties.TryGetValue("Type", out var type);
            if (fessieId is null || type is null) continue;
            
            var createdTile = new Tile()
            {
                GlobalGid = globalGid,
                Type = (TileType) Enum.Parse(typeof(TileType), type, true),
                FessieId = Int32.Parse(fessieId)
            };

            mapping[globalGid] = createdTile;
        }
        return mapping;
    }
    
    private static (Dictionary<string, Tile>, Dictionary<string, Tile>) LoadTilesetForBuildingLevel(string externalSourceFilepath, int firstTilesetGid)
    {
        
        var tileset = XElement.Load(externalSourceFilepath).Elements("tile");
        var wallMapping = new Dictionary<string, Tile>();
        var entityMapping = new Dictionary<string, Tile>();
        foreach (var tile in tileset)
        {
            var localGid = tile.Attribute("id")!.Value;
            var globalGid = (firstTilesetGid + Int32.Parse(localGid)).ToString();
            var properties = GetProperties(tile);
            properties.TryGetValue("FessieId", out var fessieId);
            properties.TryGetValue("Type", out var type);
            if (fessieId is null || type is null) continue;
            
            var createdTile = new Tile()
            {
                GlobalGid = globalGid,
                Type = (TileType) Enum.Parse(typeof(TileType), type, true),
                FessieId = Int32.Parse(fessieId)
            };

            if (createdTile.Type == Wall)
            {
                wallMapping[fessieId] = createdTile;
            }
            else
            {
                entityMapping[fessieId] = createdTile;
            }
        }

        return (wallMapping, entityMapping);
    }

    private static List<List<string>> CollectTmxLevelChunks(IEnumerable<XElement> chunkElements)
    {
        // collect chunks
        var chunks = chunkElements.Select(chunk => new Chunk()
        {
            X = Int32.Parse(chunk.Attribute("x").Value),
            Y = Int32.Parse(chunk.Attribute("y").Value),
            Width = Int32.Parse(chunk.Attribute("width").Value),
            Height = Int32.Parse(chunk.Attribute("height").Value),
            RawDataRows = chunk.Value.Split("\n").Where(row => row.Length > 0).ToArray()
        });
        
        // figure out total map rectangle size from chunks
        var minCellX = chunks.OrderBy(chunk => chunk.X).First().X;
        var minCellY = chunks.OrderBy(chunk => chunk.Y).First().Y;
        var lastX = chunks.OrderBy(chunk => chunk.X).Last();
        var maxCellX = lastX.X + lastX.Width;
        var lastY = chunks.OrderBy(chunk => chunk.Y).Last();
        var maxCellY = lastY.Y + lastX.Height;
        var totalRectMapSize = new { Width = maxCellX - minCellX, Height = maxCellY - minCellY };
        var mapRows = new List<List<string>>();
        for (var i = 0; i < totalRectMapSize.Height; i++)
        {
            mapRows.Add(new List<string>(Enumerable.Repeat("0", totalRectMapSize.Width)));
        }

        // loop through chunks and fill data
        foreach (var chunk in chunks)
        {
            foreach (var row in chunk.RawDataRows.Select((value, index) => new { value, index }))
            {
                foreach (var data in row.value.Split(",").Where(id => id.Length > 0).Select((value, index) => new { value, index }))
                {
                    mapRows[chunk.Y - minCellY + row.index][chunk.X - minCellX + data.index] = data.value;
                }
            }
        }
        
        return mapRows;
    }

    private static List<List<string>> OptimizeLevelSize(List<List<string>> levelData)
    {
        var bounds = new Bounds()
        {
            MinX = Int32.MaxValue,
            MaxX = Int32.MinValue,
            MinY = Int32.MaxValue,
            MaxY = Int32.MinValue,
        };
        
        foreach (var row in levelData.Select((value, index) => new { value, index }))
        {
            var firstX = row.value
                .Select((value, index) => new { value, index })
                .Where(gid => gid.value != TMX_GID_EMPTY)
                .Select(gid => gid.index)
                .FirstOrDefault(Int32.MaxValue);
            
            var lastX = row.value
                .Select((value, index) => new { value, index })
                .Where(gid => gid.value != TMX_GID_EMPTY)
                .Select(gid => gid.index)
                .LastOrDefault(-1);
            
            if (firstX < bounds.MinX)
            {
                bounds.MinX = firstX;
            }
            
            if (lastX > bounds.MaxX)
            {
                bounds.MaxX = lastX;
            }
            
            if (firstX != Int32.MaxValue && row.index < bounds.MinY)
            {
                bounds.MinY = row.index;
            }

            if (firstX != Int32.MaxValue && row.index > bounds.MaxY)
            {
                bounds.MaxY = row.index;
            }
        }

        if (bounds.MinX == Int32.MaxValue)
        {
            return new List<List<string>>();
        }

        // put together new more elegant data.
        var newData = new List<List<string>>();
        var relevantDataRows = levelData
            .Select((value, index) => new { value, index })
            .Where(row => row.index >= bounds.MinY && row.index <= bounds.MaxY);
        foreach (var row in relevantDataRows)
        {
            newData.Add(row.value.Slice(bounds.MinX, bounds.MaxX - bounds.MinX + 1));
        }
        return newData;
    }

    private static string GetFilepathOrFolderWithRelativeFilepath(string path, string directoryInstead) {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var combined = Path.Combine(Path.GetDirectoryName(directoryInstead) ?? "", path);
        var qualifiedPath = Path.GetFullPath(combined);
        return qualifiedPath;
    }
        

    public static FessieLevel? ParseLevel(string inputFilepath)
    {
        var level = new FessieLevel();
        var map = XElement.Load(inputFilepath);
        
        // get map properties
        var properties = GetProperties(map);
        if (!properties.ContainsKey("Zeitlimit") || !properties.ContainsKey("Müll"))
        {
            MessageBox.Show($"Level {inputFilepath} hat Zeiltimit oder Müll nicht als Karteneigenschaften gesetzt. Der Konverter nimmt humane Werte stattdessen an.",
                "Missing Map Properties", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        level.TimeLimit = properties.ContainsKey("Zeitlimit") ? Int32.Parse(properties["Zeitlimit"]) : 280;
        level.TrashRequirement = properties.ContainsKey("Müll") ? Int32.Parse(properties["Müll"]) : 0;
        level.Width = Int32.Parse(map.Attribute("width")!.Value);
        level.Height = Int32.Parse(map.Attribute("height")!.Value);
        var isEndless = map.Attribute("infinite")!.Value == "1";
        
        // get layer
        var layers = map.Elements("layer");
        var fessieLayer = layers.Count() == 1
            ? layers.First()
            : layers.FirstOrDefault(layer => layer.Attribute("name")?.Value == "Level");
        if (fessieLayer is null)
        {
            MessageBox.Show($"Level {inputFilepath} hat keine Kachelebene namens 'Level', aber auch nicht genau eine Kachelebene. Parsing des Levels wird übersprungen.",
                "Uneindeutiges Levelformat", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        var fessieLayerDataTag = fessieLayer.Element("data");
        var fessieLayerEncoding = fessieLayerDataTag?.Attribute("encoding")?.Value;
        if (fessieLayerDataTag is null || fessieLayerEncoding is not "csv")
        {
            MessageBox.Show($"Level {inputFilepath} ist nicht als CSV enkodiert. Parsing des Levels wird übersprungen.",
                "Falsches Levelformat", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
        
        // load external tilesets. and merge them into one big one. Embedded ones have no source and are not supported.
        var compatibleTilesetReferences = map.Elements("tileset").Where(ts =>
            ts.Attribute("source") is not null
            && Path.Exists(GetFilepathOrFolderWithRelativeFilepath(ts.Attribute("source")!.Value, inputFilepath)));
        var mergedGlobalTileMapping = new Dictionary<string, Tile>();
        Console.WriteLine("how many compatbible tileset refs do i have? {0}", compatibleTilesetReferences.Count());
        if (compatibleTilesetReferences.Count() == 0)
        {
            MessageBox.Show($"Level {inputFilepath} hat keine kompatiblen Tileset-Referenzen. Entweder weil die Tilesets nicht extern sind oder weil sie nicht am richtigen Pfad hinterlegt sind. Parsing des Levels wird übersprungen.",
                "Fehlende Tileset-Referenz", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
        foreach (var tilesetReference in compatibleTilesetReferences)
        {
            var firstGid = Int32.Parse(tilesetReference.Attribute("firstgid")!.Value);
            var tileMapping = LoadExternalTileset(GetFilepathOrFolderWithRelativeFilepath(tilesetReference.Attribute("source")!.Value, inputFilepath), firstGid, out var numGidsInTileset);
            mergedGlobalTileMapping.SetValues(tileMapping);
        }

        // we support only endless mode so far. data is encoded in chunks then
        if (!isEndless)
        {
            MessageBox.Show($"Level {inputFilepath} ist nicht vom Größentyp Unbegrenzt. Momentan wird kein anderer Typ unterstützt. Parsing des Levels wird übersprungen.",
                "Uneindeutiges Levelformat", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var chunkElements = fessieLayerDataTag.Elements("chunk");
        if (chunkElements.Count() == 0)
        {
            MessageBox.Show($"Level {inputFilepath} hat inkompatible Level-Daten. Chunks fehlen im Endlos-Format.",
                "Falsches Levelformat", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
        
        var mergedMapData = CollectTmxLevelChunks(chunkElements);
        var mapData = OptimizeLevelSize(mergedMapData);
        if (mapData.Count == 0)
        {
            return level;
        }
        level.Height = mapData.Count();
        level.Width = mapData.First().Count();
        var entities = new List<Entity>();
        var wallData = new List<List<string>>();
        foreach (var row in mapData.Select((value, index) => new { value, index }))
        {
            var walls = row.value.Select((gid, cellIndex) =>
            {
                if (!mergedGlobalTileMapping.ContainsKey(gid))
                {
                    return Empty.ToString("D");
                }
                
                var tile = mergedGlobalTileMapping[gid];

                if (tile.Type == TileType.Entity)
                {
                    entities.Add(new Entity()
                    {
                        Id = tile.FessieId,
                        X = cellIndex,
                        Y = row.index
                    });
                }
                return tile.Type != Wall ? Empty.ToString("D") : tile.FessieId.ToString();
            });
            wallData.Add(walls.ToList());
        }
        level.Walls = wallData.SelectMany(row => row.Select(id => Int32.Parse(id))).ToArray();
        level.Entities = entities.ToArray();
    

        return level;
    }

    static void TmxWriteLayer(this XmlWriter writer, string id, string name, string width, string height, string data)
    {
        writer.WriteStartElement("layer");
        writer.WriteAttributeString("id", id);
        writer.WriteAttributeString("name", name);
        writer.WriteAttributeString("width", width);
        writer.WriteAttributeString("height", height);
        writer.WriteStartElement("data");
        writer.WriteAttributeString("encoding", "csv");
        writer.WriteStartElement("chunk");
        writer.WriteAttributeString("x", "0");
        writer.WriteAttributeString("y", "0");
        writer.WriteAttributeString("width", width);
        writer.WriteAttributeString("height", height);
        writer.WriteValue(data);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    public static void BuildLevel(FessieLevel level, string outputFilepath, string fessieTilesetPath)
    {
        using var writer = XmlWriter.Create(outputFilepath);
        writer.WriteStartDocument();
        
        // map header
        writer.WriteStartElement("map");
        writer.WriteAttributeString("version", "1.10");
        writer.WriteAttributeString("tiledversion", "1.11.2");
        writer.WriteAttributeString("orientation", "orthogonal");
        writer.WriteAttributeString("renderorder", "right-down");
        writer.WriteAttributeString("width", level.Width.ToString());
        writer.WriteAttributeString("height", level.Height.ToString());
        writer.WriteAttributeString("tilewidth", "64");
        writer.WriteAttributeString("tileheight", "64");
        writer.WriteAttributeString("infinite", "1");
        writer.WriteAttributeString("nextlayerid", "3");
        writer.WriteAttributeString("nextobjectid", "1");
        
        // write properties
        writer.WriteStartElement("properties");
        writer.WriteStartElement("property");
        writer.WriteAttributeString("name", "Zeitlimit");
        writer.WriteAttributeString("type", "int");
        writer.WriteAttributeString("value", level.TimeLimit.ToString());
        writer.WriteEndElement();
        writer.WriteStartElement("property");
        writer.WriteAttributeString("name", "Müll");
        writer.WriteAttributeString("type", "int");
        writer.WriteAttributeString("value", level.TrashRequirement.ToString());
        writer.WriteEndElement();
        writer.WriteEndElement();
        
        // prepare wall layer with tileset
        var firstGid = 1;
        var (wallTileMapping, entityTileMapping) = LoadTilesetForBuildingLevel(fessieTilesetPath, firstGid);
        // we hardcode the EMPTY game wall id with TMX's gid for EMPTY.
        wallTileMapping[Empty.ToString("D")] = new Tile()
        {
            Type = Wall,
            FessieId = Int32.Parse(Empty.ToString("D")),
            GlobalGid = TMX_GID_EMPTY
        };

        
        // Write one big chunk. loop through all walls in fessielevel. And convert to TMX id.
        var wallRows = new List<List<string>>();
        for (var i = 0; i < level.Height; i++)
        {
            var row = new List<string>();
            for (var j = 0; j < level.Width; j++)
            {
                var wallId = level.Walls[i * level.Width + j];
                var entity = level.Entities.FirstOrDefault(entity => entity.X == j && entity.Y == i);
                var gid = TMX_GID_EMPTY;
                if (entity is not null)
                {
                    gid = entityTileMapping[entity.Id.ToString()].GlobalGid;
                }
                else if(wallTileMapping.ContainsKey(wallId.ToString()))
                {
                    gid = wallTileMapping[wallId.ToString()].GlobalGid;
                }
                else
                {
                    MessageBox.Show($"Interessant, Wall Id {wallId.ToString()} wird verwendet. Du kannst ein Github Issue aufmachen um es mich wissen zu lassen. Der Converter erwartet diese ID noch nicht.",
                        "Falsches Levelformat", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                
                row.Add(gid);
            }
            wallRows.Add(row);
        }
        var wallData = $"\n{string.Join(",\n", wallRows.Select(row => string.Join(",", row)))}\n";
            
        writer.WriteStartElement("tileset");
        writer.WriteAttributeString("firstgid", "1");
        writer.WriteAttributeString("source", fessieTilesetPath);
        writer.WriteEndElement();
        writer.TmxWriteLayer("1", "Level", level.Width.ToString(), level.Height.ToString(), wallData);
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
    }
}