namespace Basic6502.Core;

/// <summary>
/// Represents a component mapped into the 6502 address space.
/// </summary>
public interface IMemoryMappedDevice
{
    bool HandlesAddress(ushort address);
    byte Read(ushort address);
    void Write(ushort address, byte value);
}
