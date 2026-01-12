using System.Linq;

namespace Obfuscator.Emit;

internal static class CSharpName
{
    private static readonly Dictionary<Type, string> KnownAliases = new()
    {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(char)] = "char",
        [typeof(string)] = "string",
        [typeof(object)] = "object"
    };

    public static string Get(Type type)
    {
        if (KnownAliases.TryGetValue(type, out var alias))
        {
            return alias;
        }

        if (type.IsByRef)
        {
            return Get(type.GetElementType()!);
        }

        if (type.IsArray)
        {
            return $"{Get(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            return GetGenericName(type);
        }

        if (type.IsNested)
        {
            return GetNestedName(type);
        }

        return type.FullName ?? type.Name;
    }

    private static string GetNestedName(Type type)
    {
        var declaring = type.DeclaringType;
        if (declaring is null)
        {
            return type.FullName ?? type.Name;
        }

        return $"{Get(declaring)}.{type.Name}";
    }

    private static string GetGenericName(Type type)
    {
        var definition = type.GetGenericTypeDefinition();
        var name = definition.FullName ?? definition.Name;
        name = name.Split('`')[0].Replace('+', '.');
        var arguments = string.Join(", ", type.GetGenericArguments().Select(Get));
        return $"{name}<{arguments}>";
    }
}
