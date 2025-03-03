
using System.IO;
using Newtonsoft.Json;

namespace FessieLevelConverter.Parser;

using System.Xml;
using System.Xml.Linq;
using FessieLevelConverter.Fessie;

public static class JsonParser
{
    public static FessieLevel ParseLevel(string inputFilepath)
    {
        var level = JsonConvert.DeserializeObject(inputFilepath) as FessieLevel;
        return level;
    }

    public static void BuildLevel(FessieLevel fessieLevel, string outputFilepath)
    {
        string output = JsonConvert.SerializeObject(fessieLevel);
        File.WriteAllText(outputFilepath, output);
    }
}