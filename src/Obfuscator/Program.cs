using Obfuscator.Emit;

namespace Obfuscator;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: obfuscator <input-assembly-path> <output-cs-path>");
            return 1;
        }

        var inputPath = Path.GetFullPath(args[0]);
        var outputPath = Path.GetFullPath(args[1]);

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Input assembly not found: {inputPath}");
            return 1;
        }

        var emitter = new IlObfuscationEmitter();
        var source = emitter.EmitFromAssembly(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, source);

        Console.WriteLine($"Obfuscated C# source written to {outputPath}");
        return 0;
    }
}
