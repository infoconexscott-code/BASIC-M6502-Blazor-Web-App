using System;
using System.Linq;
using Basic6502.Core;
using Xunit;

namespace Basic6502.Core.UnitTests;

public class MemoryBusTests
{
    [Fact]
    public void LoadCopiesDataIntoRam()
    {
        var bus = new MemoryBus();
        var program = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        bus.Load(0x0200, program);

        for (var i = 0; i < program.Length; i++)
        {
            Assert.Equal(program[i], bus.Read((ushort)(0x0200 + i)));
        }
    }

    [Fact]
    public void ClearResetsAllRamBytes()
    {
        var bus = new MemoryBus();
        bus.Load(0x0000, Enumerable.Repeat((byte)0xFF, 16).ToArray());

        bus.Clear();

        for (ushort address = 0; address < 16; address++)
        {
            Assert.Equal(0, bus.Read(address));
        }
    }

    [Fact]
    public void LoadThrowsWhenProgramExceedsRam()
    {
        var bus = new MemoryBus(ramSize: 0x10);
        var oversized = Enumerable.Repeat((byte)0xAA, 0x20).ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => bus.Load(0x0008, oversized));
    }

    [Fact]
    public void ReadAndWriteAreForwardedToAttachedDevices()
    {
        var device = new EchoDevice(0xF000);
        var bus = new MemoryBus();
        bus.AttachDevice(device);

        bus.Write(0xF000, 0x42);

        Assert.Equal(0x42, bus.Read(0xF000));
        Assert.True(device.WasWrittenTo);
    }

    private sealed class EchoDevice : IMemoryMappedDevice
    {
        private readonly ushort _address;
        private byte _value;

        public EchoDevice(ushort address)
        {
            _address = address;
        }

        public bool WasWrittenTo { get; private set; }

        public bool HandlesAddress(ushort address) => address == _address;

        public byte Read(ushort address)
        {
            return HandlesAddress(address) ? _value : (byte)0;
        }

        public void Write(ushort address, byte value)
        {
            if (HandlesAddress(address))
            {
                WasWrittenTo = true;
                _value = value;
            }
        }
    }
}
