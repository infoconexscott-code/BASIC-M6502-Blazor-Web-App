namespace Basic6502.Core;

/// <summary>
/// The various addressing modes supported by the MOS 6502 CPU.
/// </summary>
public enum AddressingMode
{
    Implicit,
    Accumulator,
    Immediate,
    ZeroPage,
    ZeroPageX,
    ZeroPageY,
    Relative,
    Absolute,
    AbsoluteX,
    AbsoluteY,
    Indirect,
    IndexedIndirect,
    IndirectIndexed
}
