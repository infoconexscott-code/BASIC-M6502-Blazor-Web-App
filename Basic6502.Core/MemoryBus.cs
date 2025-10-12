using System.Collections.ObjectModel;

namespace Basic6502.Core;

/// <summary>
/// Default implementation of a 6502 memory bus with 64 KiB of address space.
/// </summary>
public sealed class MemoryBus : IMemoryBus
{
    private readonly byte[] _ram;
    private readonly List<IMemoryMappedDevice> _devices = new();

    public MemoryBus(int ramSize = 64 * 1024)
    {
        if (ramSize <= 0 || ramSize > 64 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(ramSize), "RAM must be between 1 and 64 KiB.");
        }

        _ram = new byte[ramSize];
    }

    /// <summary>
    /// Gets a read-only view over the internal RAM buffer.
    /// </summary>
    public ReadOnlyMemory<byte> Ram => new(_ram);

    /// <summary>
    /// Registers an additional memory mapped device.
    /// </summary>
    public void AttachDevice(IMemoryMappedDevice device)
    {
        if (device is null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        _devices.Add(device);
    }

    public byte Read(ushort address)
    {
        foreach (var device in _devices)
        {
            if (device.HandlesAddress(address))
            {
                return device.Read(address);
            }
        }

        if (address < _ram.Length)
        {
            return _ram[address];
        }

        return 0xFF;
    }

    public void Write(ushort address, byte value)
    {
        foreach (var device in _devices)
        {
            if (device.HandlesAddress(address))
            {
                device.Write(address, value);
                return;
            }
        }

        if (address < _ram.Length)
        {
            _ram[address] = value;
        }
    }

    /// <summary>
    /// Copies the provided data into RAM starting at the supplied address.
    /// </summary>
    public void Load(ushort startAddress, ReadOnlySpan<byte> data)
    {
        if (startAddress + data.Length > _ram.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Program exceeds RAM size.");
        }

        data.CopyTo(_ram.AsSpan(startAddress));
    }

    /// <summary>
    /// Resets the RAM buffer to all zeroes.
    /// </summary>
    public void Clear() => Array.Clear(_ram);
}
