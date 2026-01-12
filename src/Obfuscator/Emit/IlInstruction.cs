using System.Reflection.Emit;

namespace Obfuscator.Emit;

internal sealed record IlInstruction(int Offset, OpCode OpCode, object? Operand);
