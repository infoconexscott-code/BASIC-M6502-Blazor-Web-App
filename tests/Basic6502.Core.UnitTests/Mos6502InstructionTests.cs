using Basic6502.Core;
using Xunit;

namespace Basic6502.Core.UnitTests;

public class Mos6502InstructionTests
{
    [Fact]
    public void ResetLoadsProgramCounterFromResetVector()
    {
        var bus = new MemoryBus();
        bus.Load(0xFFFC, new byte[] { 0x34, 0x12 });

        var cpu = new Mos6502(bus);

        Assert.Equal(0x1234, cpu.State.ProgramCounter);
    }

    [Fact]
    public void LdaImmediateSetsAccumulatorAndZeroFlag()
    {
        var cpu = CreateCpu(0xA9, 0x00, 0x00);

        cpu.Step();

        var state = cpu.State;
        Assert.Equal(0, state.A);
        Assert.True(state.Status.HasFlag(StatusFlag.Zero));
        Assert.False(state.Status.HasFlag(StatusFlag.Negative));
    }

    [Fact]
    public void LdaImmediateSetsNegativeFlagWhenHighBitIsSet()
    {
        var cpu = CreateCpu(0xA9, 0x80, 0x00);

        cpu.Step();

        var state = cpu.State;
        Assert.Equal(0x80, state.A);
        Assert.True(state.Status.HasFlag(StatusFlag.Negative));
        Assert.False(state.Status.HasFlag(StatusFlag.Zero));
    }

    [Fact]
    public void AdcImmediateSetsOverflowAndNegativeFlags()
    {
        var cpu = CreateCpu(0xA9, 0x50, 0x69, 0x50, 0x00);

        cpu.Step(); // LDA
        cpu.Step(); // ADC

        var state = cpu.State;
        Assert.Equal(0xA0, state.A);
        Assert.True(state.Status.HasFlag(StatusFlag.Overflow));
        Assert.True(state.Status.HasFlag(StatusFlag.Negative));
        Assert.False(state.Status.HasFlag(StatusFlag.Carry));
    }

    [Fact]
    public void BeqBranchesWhenZeroFlagIsSet()
    {
        var cpu = CreateCpu(
            0xA9, 0x00,       // LDA #$00
            0xF0, 0x02,       // BEQ skip
            0xA9, 0x01,       // LDA #$01 (skipped)
            0x00,             // BRK (skipped)
            0xA9, 0x05,       // LDA #$05
            0x00);            // BRK

        cpu.Step(); // LDA #$00
        cpu.Step(); // BEQ (branch taken)
        cpu.Step(); // LDA #$05

        var state = cpu.State;
        Assert.Equal(0x05, state.A);
        Assert.Equal(0x8008, state.ProgramCounter);
    }

    private static Mos6502 CreateCpu(params byte[] program)
    {
        const ushort startAddress = 0x8000;
        var bus = new MemoryBus();
        bus.Load(startAddress, program);
        bus.Load(0xFFFC, new byte[] { (byte)(startAddress & 0xFF), (byte)(startAddress >> 8) });
        return new Mos6502(bus);
    }
}
