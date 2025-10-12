using System.IO;
using Basic6502.Core;
using Xunit;

namespace Basic6502.EndToEndTests;

public class BasicInterpreterEndToEndTests
{
    [Fact]
    public void ProgramEchoesQueuedInputToConsole()
    {
        var bus = new MemoryBus();
        using var writer = new StringWriter();
        var bridge = new BasicConsoleBridge(writer);
        bus.AttachDevice(bridge);

        var program = new byte[]
        {
            0xAD, 0x04, 0xF0, // LOOP: LDA $F004 (input status)
            0xF0, 0xFB,       // BEQ LOOP
            0xAD, 0x05, 0xF0, // LDA $F005 (input data)
            0x8D, 0x01, 0xF0, // STA $F001 (output)
            0xAD, 0x04, 0xF0, // LDA $F004 (input status)
            0xF0, 0x03,       // BEQ EXIT
            0x4C, 0x00, 0x80, // JMP LOOP
            0x00              // EXIT: BRK
        };

        bus.Load(0x8000, program);
        bus.Load(0xFFFC, new byte[] { 0x00, 0x80 });

        bridge.SubmitInput("HI");

        var cpu = new Mos6502(bus);
        for (var i = 0; i < 256 && writer.GetStringBuilder().Length < 2; i++)
        {
            cpu.Step();
        }

        Assert.Equal("HI", writer.ToString());
    }

    [Fact]
    public void ProgramConsumesInputFromConsoleReaderWhenQueueIsEmpty()
    {
        var bus = new MemoryBus();
        using var writer = new StringWriter();
        using var reader = new StringReader("OK");
        var bridge = new BasicConsoleBridge(writer, reader);
        bus.AttachDevice(bridge);

        var program = new byte[]
        {
            0xAD, 0x05, 0xF0, // LDA $F005 (input data)
            0x8D, 0x01, 0xF0, // STA $F001 (output)
            0xAD, 0x05, 0xF0, // LDA $F005 (input data)
            0x8D, 0x01, 0xF0, // STA $F001 (output)
            0x00              // BRK
        };

        bus.Load(0x8000, program);
        bus.Load(0xFFFC, new byte[] { 0x00, 0x80 });

        var cpu = new Mos6502(bus);
        for (var i = 0; i < 32 && writer.GetStringBuilder().Length < 2; i++)
        {
            cpu.Step();
        }

        Assert.Equal("OK", writer.ToString());
    }
}
