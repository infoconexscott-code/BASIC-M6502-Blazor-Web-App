namespace Basic6502.Core;

/// <summary>
/// Describes a component capable of servicing reads and writes for the CPU.
/// </summary>
public interface IMemoryBus
{
    byte Read(ushort address);
    void Write(ushort address, byte value);
}
