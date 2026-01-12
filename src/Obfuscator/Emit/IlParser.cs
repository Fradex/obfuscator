using System.Reflection;
using System.Reflection.Emit;

namespace Obfuscator.Emit;

internal sealed class IlParser
{
    private readonly Dictionary<short, OpCode> _opcodeMap;

    public IlParser()
    {
        _opcodeMap = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(code => code.Value);
    }

    public IReadOnlyList<IlInstruction> Parse(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            return Array.Empty<IlInstruction>();
        }

        var il = body.GetILAsByteArray();
        if (il is null || il.Length == 0)
        {
            return Array.Empty<IlInstruction>();
        }

        var module = method.Module;
        var typeArgs = method.DeclaringType?.GetGenericArguments() ?? Type.EmptyTypes;
        var methodArgs = method is MethodInfo methodInfo && methodInfo.IsGenericMethod
            ? methodInfo.GetGenericArguments()
            : Type.EmptyTypes;

        var instructions = new List<IlInstruction>();
        var offset = 0;

        while (offset < il.Length)
        {
            var startOffset = offset;
            var opCode = ReadOpCode(il, ref offset);
            var opCodeSize = offset - startOffset;
            var operand = ReadOperand(opCode, il, ref offset, module, typeArgs, methodArgs, startOffset, opCodeSize);
            instructions.Add(new IlInstruction(startOffset, opCode, operand));
        }

        return instructions;
    }

    private OpCode ReadOpCode(byte[] il, ref int offset)
    {
        var code = il[offset++];
        if (code == 0xFE)
        {
            var second = il[offset++];
            return _opcodeMap[(short)(0xFE00 | second)];
        }

        return _opcodeMap[code];
    }

    private static object? ReadOperand(
        OpCode opCode,
        byte[] il,
        ref int offset,
        Module module,
        Type[] typeArgs,
        Type[] methodArgs,
        int instructionOffset,
        int opCodeSize)
    {
        return opCode.OperandType switch
        {
            OperandType.InlineNone => null,
            OperandType.ShortInlineI => (sbyte)il[offset++],
            OperandType.InlineI => ReadInt32(il, ref offset),
            OperandType.InlineI8 => ReadInt64(il, ref offset),
            OperandType.ShortInlineR => ReadSingle(il, ref offset),
            OperandType.InlineR => ReadDouble(il, ref offset),
            OperandType.InlineString => module.ResolveString(ReadInt32(il, ref offset)),
            OperandType.InlineBrTarget => ReadBranchTarget(il, ref offset, instructionOffset, opCodeSize, 4),
            OperandType.ShortInlineBrTarget => ReadBranchTarget(il, ref offset, instructionOffset, opCodeSize, 1),
            OperandType.InlineSwitch => ReadSwitchTargets(il, ref offset, instructionOffset, opCodeSize),
            OperandType.InlineMethod => module.ResolveMethod(ReadInt32(il, ref offset), typeArgs, methodArgs),
            OperandType.InlineField => module.ResolveField(ReadInt32(il, ref offset), typeArgs, methodArgs),
            OperandType.InlineType => module.ResolveType(ReadInt32(il, ref offset), typeArgs, methodArgs),
            OperandType.InlineTok => module.ResolveMember(ReadInt32(il, ref offset), typeArgs, methodArgs),
            OperandType.InlineSig => throw new NotSupportedException("InlineSig operands are not supported."),
            OperandType.InlineVar => (short)ReadInt16(il, ref offset),
            OperandType.ShortInlineVar => (byte)il[offset++],
            _ => throw new NotSupportedException($"Unsupported operand type: {opCode.OperandType}")
        };
    }

    private static int ReadBranchTarget(byte[] il, ref int offset, int instructionOffset, int opCodeSize, int operandSize)
    {
        var delta = operandSize == 1 ? (sbyte)il[offset++] : ReadInt32(il, ref offset);
        var nextOffset = instructionOffset + opCodeSize + operandSize;
        return nextOffset + delta;
    }

    private static int[] ReadSwitchTargets(byte[] il, ref int offset, int instructionOffset, int opCodeSize)
    {
        var count = ReadInt32(il, ref offset);
        var baseOffset = instructionOffset + opCodeSize + 4 + (4 * count);
        var targets = new int[count];
        for (var i = 0; i < count; i++)
        {
            targets[i] = baseOffset + ReadInt32(il, ref offset);
        }

        return targets;
    }

    private static short ReadInt16(byte[] il, ref int offset)
    {
        var value = BitConverter.ToInt16(il, offset);
        offset += 2;
        return value;
    }

    private static int ReadInt32(byte[] il, ref int offset)
    {
        var value = BitConverter.ToInt32(il, offset);
        offset += 4;
        return value;
    }

    private static long ReadInt64(byte[] il, ref int offset)
    {
        var value = BitConverter.ToInt64(il, offset);
        offset += 8;
        return value;
    }

    private static float ReadSingle(byte[] il, ref int offset)
    {
        var value = BitConverter.ToSingle(il, offset);
        offset += 4;
        return value;
    }

    private static double ReadDouble(byte[] il, ref int offset)
    {
        var value = BitConverter.ToDouble(il, offset);
        offset += 8;
        return value;
    }
}
