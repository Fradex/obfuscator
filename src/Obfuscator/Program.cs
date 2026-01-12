using System.Collections.Concurrent;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Obfuscator;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: obfuscator <input_sln_directory> <output_directory>");
            return 1;
        }

        var inputDirectory = Path.GetFullPath(args[0]);
        var outputDirectory = Path.GetFullPath(args[1]);

        if (!Directory.Exists(inputDirectory))
        {
            Console.Error.WriteLine($"Input directory not found: {inputDirectory}");
            return 1;
        }

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        Directory.CreateDirectory(outputDirectory);

        var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var obfuscator = new SourceObfuscator();
        var exceptions = new ConcurrentQueue<Exception>();

        Parallel.ForEach(files, filePath =>
        {
            try
            {
                var relative = Path.GetRelativePath(inputDirectory, filePath);
                var targetPath = Path.Combine(outputDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                if (Path.GetExtension(filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var source = File.ReadAllText(filePath, Encoding.UTF8);
                    var obfuscated = obfuscator.Obfuscate(source, filePath);
                    File.WriteAllText(targetPath, obfuscated, Encoding.UTF8);
                }
                else
                {
                    File.Copy(filePath, targetPath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(new InvalidOperationException($"Failed to process {filePath}", ex));
            }
        });

        if (!exceptions.IsEmpty)
        {
            foreach (var exception in exceptions)
            {
                Console.Error.WriteLine(exception.ToString());
            }

            return 1;
        }

        Console.WriteLine($"Obfuscated sources written to: {outputDirectory}");
        return 0;
    }
}

internal sealed class SourceObfuscator
{
    public string Obfuscate(string sourceText, string filePath)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceText, path: filePath, options: new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "Obfuscation",
            new[] { tree },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
        var root = tree.GetRoot();

        var renamer = new LocalRenamer(semanticModel);
        var renamedRoot = renamer.Visit(root);
        if (renamedRoot is null)
        {
            return sourceText;
        }

        var stripped = renamedRoot.WithoutTrivia();
        return stripped.NormalizeWhitespace(" ", " ").ToFullString();
    }
}

internal sealed class LocalRenamer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<ISymbol, string> _nameMap = new(SymbolEqualityComparer.Default);
    private int _localCounter;
    private int _localFunctionCounter;

    public LocalRenamer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol is ILocalSymbol)
        {
            var name = GetOrCreateName(symbol, "v", ref _localCounter);
            var identifier = SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, name, node.Identifier.TrailingTrivia);
            node = node.WithIdentifier(identifier);
        }

        return base.VisitVariableDeclarator(node);
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction })
        {
            var name = GetOrCreateName(symbol, "f", ref _localFunctionCounter);
            var identifier = SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, name, node.Identifier.TrailingTrivia);
            node = node.WithIdentifier(identifier);
        }

        return base.VisitLocalFunctionStatement(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
        if (symbol is ILocalSymbol or IMethodSymbol { MethodKind: MethodKind.LocalFunction })
        {
            var name = GetOrCreateName(symbol, symbol is ILocalSymbol ? "v" : "f", symbol is ILocalSymbol ? ref _localCounter : ref _localFunctionCounter);
            var identifier = SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, name, node.Identifier.TrailingTrivia);
            return node.WithIdentifier(identifier);
        }

        return base.VisitIdentifierName(node);
    }

    private string GetOrCreateName(ISymbol symbol, string prefix, ref int counter)
    {
        if (_nameMap.TryGetValue(symbol, out var existing))
        {
            return existing;
        }

        var name = $"{prefix}{counter++}";
        _nameMap[symbol] = name;
        return name;
    }
}
