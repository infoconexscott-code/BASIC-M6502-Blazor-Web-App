using System.Collections.Concurrent;

namespace Basic6502.Core;

/// <summary>
/// Provides a simple bridge between the emulated system and a host console.
/// </summary>
public sealed class BasicConsoleBridge : IMemoryMappedDevice
{
    private readonly TextWriter _output;
    private readonly TextReader _input;
    private readonly ConcurrentQueue<char> _inputBuffer = new();

    /// <summary>
    /// Creates a new bridge instance.
    /// </summary>
    /// <param name="output">Where characters written by the emulated machine should be sent.</param>
    /// <param name="input">Optional input source. If omitted, input must be provided through <see cref="SubmitInput(string)"/>.</param>
    public BasicConsoleBridge(TextWriter? output = null, TextReader? input = null)
    {
        _output = output ?? TextWriter.Synchronized(Console.Out);
        _input = input ?? TextReader.Synchronized(Console.In);
    }

    /// <summary>
    /// Address that the CPU writes to in order to emit a character.
    /// </summary>
    public ushort OutputDataAddress { get; init; } = 0xF001;

    /// <summary>
    /// Address the CPU reads to determine whether input is available.
    /// Returns 0 when no characters are buffered and 1 otherwise.
    /// </summary>
    public ushort InputStatusAddress { get; init; } = 0xF004;

    /// <summary>
    /// Address the CPU reads from to fetch the next buffered character.
    /// </summary>
    public ushort InputDataAddress { get; init; } = 0xF005;

    public bool HandlesAddress(ushort address)
    {
        return address == OutputDataAddress || address == InputStatusAddress || address == InputDataAddress;
    }

    public byte Read(ushort address)
    {
        if (address == InputStatusAddress)
        {
            return (byte)(_inputBuffer.IsEmpty ? 0 : 1);
        }

        if (address == InputDataAddress)
        {
            if (_inputBuffer.TryDequeue(out var ch))
            {
                return (byte)ch;
            }

            int value = _input.Peek();
            if (value < 0)
            {
                return 0;
            }

            value = _input.Read();
            return value < 0 ? (byte)0 : (byte)value;
        }

        return 0;
    }

    public void Write(ushort address, byte value)
    {
        if (address == OutputDataAddress)
        {
            _output.Write((char)value);
            _output.Flush();
        }
    }

    /// <summary>
    /// Queues characters to be consumed by the 6502 program.
    /// </summary>
    public void SubmitInput(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        foreach (var ch in text)
        {
            _inputBuffer.Enqueue(ch);
        }
    }
}
