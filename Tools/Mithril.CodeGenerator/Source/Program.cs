namespace Mithril.CodeGenerator;

using System.Text;
using System.Text.RegularExpressions;

public class Program
{
  public static void Main(string[] args)
  {
    if (args.Length < 2)
    {
      Console.WriteLine("Usage: CodeGenerator <protoPath> <generatePath>");
      return;
    }

    var protoPath = args[0];
    var generatePath = args[1];

    if (Directory.Exists(protoPath) == false)
    {
      Console.WriteLine($"Directory not found: {Path.GetFullPath(protoPath)}");
      return;
    }

    if (Directory.Exists(generatePath) == false)
    {
      Directory.CreateDirectory(generatePath);
    }

    var filePaths = Directory.GetFiles(protoPath, "*.proto");

    var allPackets = new List<(string Name, ushort Id)>();

    foreach (string filePath in filePaths)
    {
      var packetsInFile = ProcessProtoFile(filePath, generatePath);
      allPackets.AddRange(packetsInFile);
    }

    GenerateMetadataFile(generatePath, allPackets);

    if (DetectCollisions(allPackets))
    {
      return;
    }

    Console.WriteLine("Code generation completed successfully.");
  }

  private static bool DetectCollisions(List<(string Name, ushort Id)> packets)
  {
    var idToName = new Dictionary<ushort, string>();
    var hasCollision = false;

    foreach (var (name, id) in packets)
    {
      if (idToName.TryGetValue(id, out var existing))
      {
        Console.Error.WriteLine($"ERROR: Packet ID collision detected! '{name}' and '{existing}' both have ID {id}");
        hasCollision = true;
      }
      else
      {
        idToName[id] = name;
      }
    }

    return hasCollision;
  }

  private static List<(string Name, ushort Id)> ProcessProtoFile(string filePath, string generatePath)
  {
    var fileName = Path.GetFileNameWithoutExtension(filePath);
    var content = File.ReadAllText(filePath);
    var matches = Regex.Matches(content, @"message\s+(\w+)");
    var packets = new List<(string Name, ushort Id)>();

    if (matches.Count == 0)
    {
      return packets;
    }

    var sb = new StringBuilder();
    sb.AppendLine("namespace Mithril.Protocol;");
    sb.AppendLine();

    foreach (Match match in matches)
    {
      string packetName = match.Groups[1].Value;
      ushort id = GetPacketIdByName(packetName);
      packets.Add((packetName, id));

      sb.AppendLine($"public partial class {packetName}");
      sb.AppendLine("{");
      sb.AppendLine($"  public const ushort Id = {id};");
      sb.AppendLine("}");
      sb.AppendLine();
    }

    var outputFileName = $"{fileName}Id.g.cs";
    File.WriteAllText(Path.Combine(generatePath, outputFileName), sb.ToString().TrimEnd() + Environment.NewLine);
    Console.WriteLine($" > Generated: {outputFileName}");

    return packets;
  }

  private static void GenerateMetadataFile(string generatePath, List<(string Name, ushort Id)> packets)
  {
    var sb = new StringBuilder();

    sb.AppendLine("namespace Mithril.Protocol;");
    sb.AppendLine();
    sb.AppendLine("using Google.Protobuf;");
    sb.AppendLine("using Mithril.Network.Packet;");
    sb.AppendLine("using System;");
    sb.AppendLine();
    sb.AppendLine("public sealed class PacketMetadata : IPacketMetadata");
    sb.AppendLine("{");
    sb.AppendLine("  public bool TryGetId<T>(out ushort id) where T : IMessage");
    sb.AppendLine("  {");
    sb.AppendLine("    id = PacketIdCache<T>.Id;");
    sb.AppendLine("    return PacketIdCache<T>.HasId;");
    sb.AppendLine("  }");
    sb.AppendLine();
    sb.AppendLine("  private static class PacketIdCache<T> where T : IMessage");
    sb.AppendLine("  {");
    sb.AppendLine("    public static readonly bool HasId;");
    sb.AppendLine("    public static readonly ushort Id;");
    sb.AppendLine();
    sb.AppendLine("    static PacketIdCache()");
    sb.AppendLine("    {");

    foreach (var (name, id) in packets.OrderBy(p => p.Name, StringComparer.Ordinal))
    {
      sb.AppendLine($"      if (typeof(T) == typeof({name}))");
      sb.AppendLine("      {");
      sb.AppendLine("        HasId = true;");
      sb.AppendLine($"        Id = {id};");
      sb.AppendLine("        return;");
      sb.AppendLine("      }");
      sb.AppendLine();
    }

    sb.AppendLine("      HasId = false;");
    sb.AppendLine("      Id = 0;");
    sb.AppendLine("    }");
    sb.AppendLine("  }");
    sb.AppendLine("}");

    Directory.CreateDirectory(generatePath);

    var outputPath = Path.Combine(generatePath, "PacketMetadata.g.cs");
    File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
  }


  private static ushort GetPacketIdByName(string name)
  {
    uint hash = 2166136261;

    foreach (char c in name)
    {
      hash = (hash ^ c) * 16777619;
    }

    return (ushort)(hash & 0xFFFF);
  }
}