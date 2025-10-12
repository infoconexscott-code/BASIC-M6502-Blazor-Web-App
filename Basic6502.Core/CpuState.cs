namespace Basic6502.Core;

/// <summary>
/// Represents the visible state of the MOS 6502 CPU.
/// </summary>
public readonly record struct CpuState(
    byte A,
    byte X,
    byte Y,
    ushort ProgramCounter,
    byte StackPointer,
    StatusFlag Status)
{
    /// <summary>
    /// Gets a state object with sensible power-on defaults.
    /// </summary>
    public static CpuState PowerOn()
    {
        return new CpuState(
            A: 0,
            X: 0,
            Y: 0,
            ProgramCounter: 0,
            StackPointer: 0xFD,
            Status: StatusFlag.Unused | StatusFlag.InterruptDisable);
    }
}
