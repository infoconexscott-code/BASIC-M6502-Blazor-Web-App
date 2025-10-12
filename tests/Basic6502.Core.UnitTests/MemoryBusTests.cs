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
}
