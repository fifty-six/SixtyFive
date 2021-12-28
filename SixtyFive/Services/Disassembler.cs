using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Iced.Intel;
using Microsoft.Diagnostics.Runtime;
using Decoder = Iced.Intel.Decoder;

namespace SixtyFive.Services
{
    public static class Disassembler
    {
        public static string DisassembleMethod(MethodBase mi)
        {
            using DataTarget target = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, uint.MaxValue, AttachFlag.Passive);

            ClrInfo clrVersion = target.ClrVersions.First();
            ClrRuntime runtime = clrVersion.CreateRuntime();

            RuntimeHelpers.PrepareMethod(mi.MethodHandle);

            ClrMethod clrHandle = runtime.GetMethodByHandle((ulong) mi.MethodHandle.Value);

            ulong ptr = clrHandle.HotColdInfo.HotStart;
            uint size = clrHandle.HotColdInfo.HotSize;

            var reader = new UnmanagedCodeReader(new IntPtr((long) ptr), size);

            var decoder = Decoder.Create(IntPtr.Size * 8, reader);

            decoder.IP = ptr;

            ulong end = decoder.IP + size;

            var instructions = new InstructionList();
            while (decoder.IP < end)
            {
                decoder.Decode(out instructions.AllocUninitializedElement());
            }

            var resolver = new JitAsmSymbolResolver(runtime, ptr, size);

            var formatter = new IntelFormatter
            (
                new FormatterOptions
                {
                    HexPrefix = "0x",
                    HexSuffix = null,
                    UppercaseHex = false,
                    SpaceAfterOperandSeparator = true,
                    SpaceBetweenMemoryAddOperators = true
                },
                resolver
            );

            var output = new StringOutput();
            var sb = new StringBuilder();

            foreach (ref var instr in instructions)
            {
                formatter.Format(instr, output);

                sb.AppendLine($"L{instr.IP - ptr:x4}: {output.ToStringAndReset()}");
            }

            return sb.ToString();
        }

        // https://github.com/xoofx/JitBuddy/blob/master/src/JitBuddy/JitBuddy.cs#L102
        private class UnmanagedCodeReader : CodeReader
        {
            private readonly IntPtr _ptr;
            private readonly uint _size;
            private uint _offset;

            public UnmanagedCodeReader(IntPtr ptr, uint size)
            {
                _ptr = ptr;
                _size = size;
            }

            public override int ReadByte()
            {
                if (_offset >= _size)
                    return -1;

                return Marshal.ReadByte(_ptr, (int) _offset++);
            }
        }

        // https://github.com/ashmind/SharpLab/blob/main/source/Server/Decompilation/Internal/JitAsmSymbolResolver.cs
        private class JitAsmSymbolResolver : ISymbolResolver
        {
            private readonly ClrRuntime _runtime;
            private readonly ulong _currentMethodAddress;
            private readonly uint _currentMethodLength;

            public JitAsmSymbolResolver(ClrRuntime runtime, ulong currentMethodAddress, uint currentMethodLength)
            {
                _runtime = runtime;
                _currentMethodAddress = currentMethodAddress;
                _currentMethodLength = currentMethodLength;
            }

            public bool TryGetSymbol
            (
                in Instruction instruction,
                int operand,
                int instructionOperand,
                ulong address,
                int addressSize,
                out SymbolResult symbol
            )
            {
                if (address >= _currentMethodAddress && address < _currentMethodAddress + _currentMethodLength)
                {
                    // Relative offset reference
                    symbol = new SymbolResult(address, "L" + (address - _currentMethodAddress).ToString("x4"));
                    return true;
                }

                ClrMethod method = _runtime.GetMethodByAddress(address);

                if (method != null)
                {
                    symbol = new SymbolResult(address, method.GetFullSignature());

                    return true;
                }

                symbol = default;

                return false;
            }
        }
    }
}