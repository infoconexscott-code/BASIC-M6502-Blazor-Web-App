using System.IO;
using Basic6502.Core;
using Xunit;

namespace Basic6502.Core.UnitTests;

public class BasicConsoleBridgeTests
{
    [Fact]
    public void WriteToOutputAddressAppendsCharacter()
    {
        using var writer = new StringWriter();
        var bridge = new BasicConsoleBridge(writer: writer);

        bridge.Write(bridge.OutputDataAddress, (byte)'Z');

        Assert.Equal("Z", writer.ToString());
    }

    [Fact]
    public void SubmitInputProvidesBufferedCharacters()
    {
        var bridge = new BasicConsoleBridge(writer: new StringWriter());

        bridge.SubmitInput("OK");

        Assert.Equal(1, bridge.Read(bridge.InputStatusAddress));
        Assert.Equal('O', bridge.Read(bridge.InputDataAddress));
        Assert.Equal('K', bridge.Read(bridge.InputDataAddress));
        Assert.Equal(0, bridge.Read(bridge.InputStatusAddress));
    }

    [Fact]
    public void ReadFallsBackToProvidedTextReaderWhenBufferIsEmpty()
    {
        using var input = new StringReader("!\n");
        var bridge = new BasicConsoleBridge(writer: new StringWriter(), input: input);

        Assert.Equal(0, bridge.Read(bridge.InputStatusAddress));
        Assert.Equal('!', bridge.Read(bridge.InputDataAddress));
        Assert.Equal('\n', bridge.Read(bridge.InputDataAddress));
        Assert.Equal(0, bridge.Read(bridge.InputDataAddress));
    }
}
