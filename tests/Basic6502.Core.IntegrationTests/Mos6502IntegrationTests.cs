using System.IO;
using Basic6502.Core;
using Xunit;

namespace Basic6502.Core.IntegrationTests;

public class Mos6502IntegrationTests
{
    [Fact]
    public void CpuProgramWritesCharacterThroughConsoleBridge()
    {
        var bus = new MemoryBus();
        using var writer = new StringWriter();
        var bridge = new BasicConsoleBridge(writer);
        bus.AttachDevice(bridge);

        var program = new byte[]
        {
            0xA9, 0x41,       // LDA #$41 ('A')
            0x8D, 0x01, 0xF0, // STA $F001
            0x00              // BRK
        };

        bus.Load(0x8000, program);
        bus.Load(0xFFFC, new byte[] { 0x00, 0x80 });

        var cpu = new Mos6502(bus);
        cpu.Step(); // LDA
        cpu.Step(); // STA -> writes to console
        cpu.Step(); // BRK

        Assert.Equal("A", writer.ToString());
    }
}
