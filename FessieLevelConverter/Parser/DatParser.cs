

using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FessieLevelConverter.Fessie;

namespace FessieLevelConverter.Parser;

public static class DatParser
{
    static Entity ReadEntity(this BinaryReader reader)
    {
        return new Entity()
        {
            Id = reader.ReadInt32(),
            X = reader.ReadInt32(),
            Y = reader.ReadInt32(),
        };
    }
    
    public static FessieLevel ParseLevel(string inputFilepath)
    {
        using var stream = File.Open(inputFilepath, FileMode.Open);
        using var reader = new BinaryReader(stream, Encoding.ASCII, false);
        
        var level = new FessieLevel();
        level.TimeLimit = reader.ReadInt32();
        level.TrashRequirement = reader.ReadInt32();
        level.Width = reader.ReadInt32();
        level.Height = reader.ReadInt32();
        level.Walls = Enumerable.Range(0, level.Width * level.Height).Select(_ => reader.ReadInt32()).ToArray();
        var countEntities = reader.ReadInt32();
        level.Entities = Enumerable.Range(0, countEntities).Select(_ => reader.ReadEntity()).ToArray();
        return level;
    }

    public static void BuildLevel(FessieLevel level, string outputFilepath)
    {
        using var stream = File.Open(outputFilepath, FileMode.OpenOrCreate);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, false);
        
        writer.Write(level.TimeLimit);
        writer.Write(level.TrashRequirement);
        writer.Write(level.Width);
        writer.Write(level.Height);
        foreach (var id in level.Walls)
        {
            writer.Write(id);
        }
        writer.Write(level.Entities.Length);
        foreach (var entity in level.Entities)
        {
            writer.Write(entity.Id);
            writer.Write(entity.X);
            writer.Write(entity.Y);
        }
        writer.Flush();
        stream.Close();
    }
}