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

    [Fact]
    public void CpuExecutesSubroutineAndReturnsToCaller()
    {
        var bus = new MemoryBus();
        var mainProgram = new byte[]
        {
            0xA9, 0x05,       // LDA #$05
            0x8D, 0x00, 0x02, // STA $0200
            0x20, 0x00, 0x90, // JSR $9000
            0x00              // BRK
        };

        var subroutine = new byte[]
        {
            0xEE, 0x00, 0x02, // INC $0200
            0xAD, 0x00, 0x02, // LDA $0200
            0x60              // RTS
        };

        bus.Load(0x8000, mainProgram);
        bus.Load(0x9000, subroutine);
        bus.Load(0xFFFC, new byte[] { 0x00, 0x80 });

        var cpu = new Mos6502(bus);

        for (var i = 0; i < 6; i++)
        {
            cpu.Step();
        }

        Assert.Equal(0x06, bus.Read(0x0200));
        Assert.Equal(0x06, cpu.State.A);
        Assert.Equal(0x8008, cpu.State.ProgramCounter);
    }
}
