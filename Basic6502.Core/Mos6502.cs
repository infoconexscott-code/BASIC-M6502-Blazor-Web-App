namespace Basic6502.Core;

/// <summary>
/// Fully featured MOS 6502 CPU emulator capable of running Microsoft BASIC.
/// </summary>
public sealed class Mos6502
{
    private readonly Instruction[] _instructionTable;
    private IMemoryBus _bus;

    private byte _a;
    private byte _x;
    private byte _y;
    private ushort _pc;
    private byte _sp;
    private StatusFlag _status;

    private bool _pageCrossed;
    private bool _branchTaken;

    private readonly struct Instruction
    {
        public Instruction(string mnemonic, AddressingMode mode, Action<Mos6502, AddressingMode> execute, int cycles, bool pageCycle = false, bool branchCycle = false)
        {
            Mnemonic = mnemonic;
            Mode = mode;
            Execute = execute;
            Cycles = cycles;
            AddsCycleOnPageCross = pageCycle;
            AddsCycleOnBranch = branchCycle;
        }

        public string Mnemonic { get; }
        public AddressingMode Mode { get; }
        public Action<Mos6502, AddressingMode> Execute { get; }
        public int Cycles { get; }
        public bool AddsCycleOnPageCross { get; }
        public bool AddsCycleOnBranch { get; }
    }

    public Mos6502(IMemoryBus bus)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _instructionTable = BuildInstructionTable();
        Reset();
    }

    public IMemoryBus Bus
    {
        get => _bus;
        set => _bus = value ?? throw new ArgumentNullException(nameof(value));
    }

    public CpuState State => new(_a, _x, _y, _pc, _sp, _status);

    public void Reset()
    {
        _a = 0;
        _x = 0;
        _y = 0;
        _sp = 0xFD;
        _status = StatusFlag.InterruptDisable | StatusFlag.Unused;
        _pc = ReadWord(0xFFFC);
    }

    public int Step()
    {
        var opcode = ReadByte(_pc++);
        var instruction = _instructionTable[opcode];
        if (instruction.Execute is null)
        {
            throw new NotSupportedException($"Opcode 0x{opcode:X2} is not implemented.");
        }

        _pageCrossed = false;
        _branchTaken = false;

        instruction.Execute(this, instruction.Mode);

        var cycles = instruction.Cycles;
        if (instruction.AddsCycleOnPageCross && _pageCrossed)
        {
            cycles++;
        }

        if (instruction.AddsCycleOnBranch && _branchTaken)
        {
            cycles++;
            if (_pageCrossed)
            {
                cycles++;
            }
        }

        return cycles;
    }

    public long Run(Func<Mos6502, bool> predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        long cycles = 0;
        while (predicate(this))
        {
            cycles += Step();
        }

        return cycles;
    }

    private byte ReadByte(ushort address) => _bus.Read(address);

    private void WriteByte(ushort address, byte value) => _bus.Write(address, value);

    private byte FetchByte()
    {
        var value = ReadByte(_pc);
        _pc++;
        return value;
    }

    private ushort FetchWord()
    {
        var lo = FetchByte();
        var hi = FetchByte();
        return (ushort)(lo | (hi << 8));
    }

    private ushort ReadWord(ushort address)
    {
        var lo = ReadByte(address);
        var hi = ReadByte((ushort)(address + 1));
        return (ushort)(lo | (hi << 8));
    }

    private ushort ReadWordBug(ushort address)
    {
        var lo = ReadByte(address);
        var hi = ReadByte((ushort)((address & 0xFF00) | ((address + 1) & 0x00FF)));
        return (ushort)(lo | (hi << 8));
    }

    private void PushByte(byte value)
    {
        WriteByte((ushort)(0x0100 | _sp), value);
        _sp--;
    }

    private void PushWord(ushort value)
    {
        PushByte((byte)(value >> 8));
        PushByte((byte)(value & 0xFF));
    }

    private byte PopByte()
    {
        _sp++;
        return ReadByte((ushort)(0x0100 | _sp));
    }

    private ushort PopWord()
    {
        var lo = PopByte();
        var hi = PopByte();
        return (ushort)(lo | (hi << 8));
    }

    private void SetFlag(StatusFlag flag, bool set)
    {
        if (set)
        {
            _status |= flag;
        }
        else
        {
            _status &= ~flag;
        }

        _status |= StatusFlag.Unused;
    }

    private bool IsFlagSet(StatusFlag flag) => (_status & flag) != 0;

    private void SetZeroAndNegative(byte value)
    {
        SetFlag(StatusFlag.Zero, value == 0);
        SetFlag(StatusFlag.Negative, (value & 0x80) != 0);
    }

    private ushort GetAddress(AddressingMode mode, out byte operand)
    {
        operand = 0;
        switch (mode)
        {
            case AddressingMode.Immediate:
                operand = FetchByte();
                return 0;
            case AddressingMode.ZeroPage:
            {
                var address = FetchByte();
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.ZeroPageX:
            {
                var address = (byte)(FetchByte() + _x);
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.ZeroPageY:
            {
                var address = (byte)(FetchByte() + _y);
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.Absolute:
            {
                var address = FetchWord();
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.AbsoluteX:
            {
                var baseAddress = FetchWord();
                var address = (ushort)(baseAddress + _x);
                _pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.AbsoluteY:
            {
                var baseAddress = FetchWord();
                var address = (ushort)(baseAddress + _y);
                _pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.Indirect:
            {
                var pointer = FetchWord();
                var address = ReadWordBug(pointer);
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.IndexedIndirect:
            {
                var pointer = (byte)(FetchByte() + _x);
                var lo = ReadByte(pointer);
                var hi = ReadByte((byte)(pointer + 1));
                var address = (ushort)(lo | (hi << 8));
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.IndirectIndexed:
            {
                var basePointer = FetchByte();
                var lo = ReadByte(basePointer);
                var hi = ReadByte((byte)(basePointer + 1));
                var baseAddress = (ushort)(lo | (hi << 8));
                var address = (ushort)(baseAddress + _y);
                _pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                operand = ReadByte(address);
                return address;
            }
            case AddressingMode.Relative:
            {
                operand = FetchByte();
                return 0;
            }
            case AddressingMode.Accumulator:
            {
                operand = _a;
                return 0;
            }
            case AddressingMode.Implicit:
                return 0;
            default:
                throw new InvalidOperationException($"Unsupported addressing mode: {mode}.");
        }
    }

    private ushort GetAddressForWrite(AddressingMode mode)
    {
        switch (mode)
        {
            case AddressingMode.ZeroPage:
                return FetchByte();
            case AddressingMode.ZeroPageX:
                return (byte)(FetchByte() + _x);
            case AddressingMode.ZeroPageY:
                return (byte)(FetchByte() + _y);
            case AddressingMode.Absolute:
                return FetchWord();
            case AddressingMode.AbsoluteX:
            {
                var baseAddress = FetchWord();
                var address = (ushort)(baseAddress + _x);
                _pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                return address;
            }
            case AddressingMode.AbsoluteY:
            {
                var baseAddress = FetchWord();
                var address = (ushort)(baseAddress + _y);
                _pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                return address;
            }
            case AddressingMode.IndexedIndirect:
            {
                var pointer = (byte)(FetchByte() + _x);
                var lo = ReadByte(pointer);
                var hi = ReadByte((byte)(pointer + 1));
                return (ushort)(lo | (hi << 8));
            }
            case AddressingMode.IndirectIndexed:
            {
                var pointer = FetchByte();
                var lo = ReadByte(pointer);
                var hi = ReadByte((byte)(pointer + 1));
                var baseAddress = (ushort)(lo | (hi << 8));
                var address = (ushort)(baseAddress + _y);
                _pageCrossed = (baseAddress & 0xFF00) != (address & 0xFF00);
                return address;
            }
            case AddressingMode.Accumulator:
                return 0;
            default:
                throw new InvalidOperationException($"Unsupported addressing mode {mode} for write.");
        }
    }

    private byte GetOperand(AddressingMode mode)
    {
        if (mode == AddressingMode.Immediate)
        {
            return FetchByte();
        }

        if (mode == AddressingMode.Accumulator)
        {
            return _a;
        }

        GetAddress(mode, out var operand);
        return operand;
    }

    private void BranchIf(bool condition)
    {
        var offset = (sbyte)FetchByte();
        if (!condition)
        {
            return;
        }

        _branchTaken = true;
        var oldPc = _pc;
        _pc = (ushort)(_pc + offset);
        _pageCrossed = (oldPc & 0xFF00) != (_pc & 0xFF00);
    }

    private static int BcdToInt(byte value) => ((value >> 4) * 10) + (value & 0x0F);

    private static byte IntToBcd(int value)
    {
        value %= 100;
        if (value < 0)
        {
            value += 100;
        }

        return (byte)(((value / 10) << 4) | (value % 10));
    }

    private void Adc(AddressingMode mode)
    {
        var operand = GetOperand(mode);
        var carryIn = IsFlagSet(StatusFlag.Carry) ? 1 : 0;
        var a = _a;
        var sum = a + operand + carryIn;
        var overflow = (~(a ^ operand) & (a ^ sum) & 0x80) != 0;

        if (IsFlagSet(StatusFlag.Decimal))
        {
            var decimalSum = BcdToInt(a) + BcdToInt(operand) + carryIn;
            SetFlag(StatusFlag.Carry, decimalSum > 99);
            _a = IntToBcd(decimalSum);
        }
        else
        {
            SetFlag(StatusFlag.Carry, sum > 0xFF);
            _a = (byte)sum;
        }

        SetFlag(StatusFlag.Overflow, overflow);
        SetZeroAndNegative(_a);
    }

    private void Sbc(AddressingMode mode)
    {
        var operand = GetOperand(mode);
        var carryIn = IsFlagSet(StatusFlag.Carry) ? 1 : 0;
        var borrow = 1 - carryIn;
        var diff = _a - operand - borrow;
        var overflow = ((_a ^ operand) & (_a ^ diff) & 0x80) != 0;

        if (IsFlagSet(StatusFlag.Decimal))
        {
            var decimalDiff = BcdToInt(_a) - BcdToInt(operand) - borrow;
            var carried = decimalDiff >= 0;
            if (!carried)
            {
                decimalDiff += 100;
            }

            SetFlag(StatusFlag.Carry, carried);
            _a = IntToBcd(decimalDiff);
        }
        else
        {
            SetFlag(StatusFlag.Carry, diff >= 0);
            _a = (byte)diff;
        }

        SetFlag(StatusFlag.Overflow, overflow);
        SetZeroAndNegative(_a);
    }

    private void And(AddressingMode mode)
    {
        _a &= GetOperand(mode);
        SetZeroAndNegative(_a);
    }

    private void Ora(AddressingMode mode)
    {
        _a |= GetOperand(mode);
        SetZeroAndNegative(_a);
    }

    private void Eor(AddressingMode mode)
    {
        _a ^= GetOperand(mode);
        SetZeroAndNegative(_a);
    }

    private void Bit(AddressingMode mode)
    {
        var value = GetOperand(mode);
        var result = (byte)(_a & value);
        SetFlag(StatusFlag.Zero, result == 0);
        SetFlag(StatusFlag.Negative, (value & 0x80) != 0);
        SetFlag(StatusFlag.Overflow, (value & 0x40) != 0);
    }

    private byte Asl(byte value)
    {
        SetFlag(StatusFlag.Carry, (value & 0x80) != 0);
        value <<= 1;
        SetZeroAndNegative(value);
        return value;
    }

    private byte Lsr(byte value)
    {
        SetFlag(StatusFlag.Carry, (value & 0x01) != 0);
        value >>= 1;
        SetZeroAndNegative(value);
        return value;
    }

    private byte Rol(byte value)
    {
        var carry = IsFlagSet(StatusFlag.Carry) ? 1 : 0;
        var newCarry = (value & 0x80) != 0;
        value = (byte)((value << 1) | carry);
        SetFlag(StatusFlag.Carry, newCarry);
        SetZeroAndNegative(value);
        return value;
    }

    private byte Ror(byte value)
    {
        var carry = IsFlagSet(StatusFlag.Carry) ? 1 : 0;
        var newCarry = (value & 0x01) != 0;
        value = (byte)((value >> 1) | (carry << 7));
        SetFlag(StatusFlag.Carry, newCarry);
        SetZeroAndNegative(value);
        return value;
    }

    private void Compare(AddressingMode mode, byte register)
    {
        var value = GetOperand(mode);
        var result = (byte)(register - value);
        SetFlag(StatusFlag.Carry, register >= value);
        SetZeroAndNegative(result);
    }

    private void Dec(AddressingMode mode)
    {
        if (mode == AddressingMode.Accumulator)
        {
            _a--;
            SetZeroAndNegative(_a);
            return;
        }

        var address = GetAddressForWrite(mode);
        var value = ReadByte(address);
        value--;
        WriteByte(address, value);
        SetZeroAndNegative(value);
    }

    private void Inc(AddressingMode mode)
    {
        if (mode == AddressingMode.Accumulator)
        {
            _a++;
            SetZeroAndNegative(_a);
            return;
        }

        var address = GetAddressForWrite(mode);
        var value = ReadByte(address);
        value++;
        WriteByte(address, value);
        SetZeroAndNegative(value);
    }

    private void Load(AddressingMode mode, ref byte register)
    {
        register = GetOperand(mode);
        SetZeroAndNegative(register);
    }

    private void Store(AddressingMode mode, byte register)
    {
        var address = GetAddressForWrite(mode);
        WriteByte(address, register);
    }

    private void Transfer(ref byte destination, byte source)
    {
        destination = source;
        SetZeroAndNegative(destination);
    }

    private void Flag(StatusFlag flag, bool set) => SetFlag(flag, set);

    private Instruction[] BuildInstructionTable()
    {
        var table = new Instruction[256];

        void Map(byte opcode, string name, AddressingMode mode, Action<Mos6502, AddressingMode> action, int cycles, bool page = false, bool branch = false)
        {
            table[opcode] = new Instruction(name, mode, action, cycles, page, branch);
        }

        void NotSupported(byte opcode)
        {
            table[opcode] = new Instruction("???", AddressingMode.Implicit, static (_, _) => throw new NotSupportedException("Illegal opcode."), 0);
        }

        for (int i = 0; i < table.Length; i++)
        {
            NotSupported((byte)i);
        }

        Map(0x00, "BRK", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._pc++;
            cpu.PushWord(cpu._pc);
            cpu.PushByte((byte)(cpu._status | StatusFlag.Break));
            cpu.SetFlag(StatusFlag.InterruptDisable, true);
            cpu._pc = cpu.ReadWord(0xFFFE);
        }, 7);

        Map(0x01, "ORA", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.Ora(mode), 6);
        Map(0x05, "ORA", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Ora(mode), 3);
        Map(0x06, "ASL", AddressingMode.ZeroPage, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Asl(value);
            cpu.WriteByte(address, value);
        }, 5);
        Map(0x08, "PHP", AddressingMode.Implicit, static (cpu, _) => cpu.PushByte((byte)(cpu._status | StatusFlag.Break)), 3);
        Map(0x09, "ORA", AddressingMode.Immediate, static (cpu, mode) => cpu.Ora(mode), 2);
        Map(0x0A, "ASL", AddressingMode.Accumulator, static (cpu, _) => cpu._a = cpu.Asl(cpu._a), 2);
        Map(0x0D, "ORA", AddressingMode.Absolute, static (cpu, mode) => cpu.Ora(mode), 4);
        Map(0x0E, "ASL", AddressingMode.Absolute, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Asl(value);
            cpu.WriteByte(address, value);
        }, 6);

        Map(0x10, "BPL", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(!cpu.IsFlagSet(StatusFlag.Negative)), 2, branch: true);
        Map(0x11, "ORA", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.Ora(mode), 5, page: true);
        Map(0x15, "ORA", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Ora(mode), 4);
        Map(0x16, "ASL", AddressingMode.ZeroPageX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Asl(value);
            cpu.WriteByte(address, value);
        }, 6);
        Map(0x18, "CLC", AddressingMode.Implicit, static (cpu, _) => cpu.Flag(StatusFlag.Carry, false), 2);
        Map(0x19, "ORA", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Ora(mode), 4, page: true);
        Map(0x1D, "ORA", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Ora(mode), 4, page: true);
        Map(0x1E, "ASL", AddressingMode.AbsoluteX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Asl(value);
            cpu.WriteByte(address, value);
        }, 7);

        Map(0x20, "JSR", AddressingMode.Absolute, static (cpu, _) =>
        {
            var target = cpu.FetchWord();
            cpu.PushWord((ushort)(cpu._pc - 1));
            cpu._pc = target;
        }, 6);
        Map(0x21, "AND", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.And(mode), 6);
        Map(0x24, "BIT", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Bit(mode), 3);
        Map(0x25, "AND", AddressingMode.ZeroPage, static (cpu, mode) => cpu.And(mode), 3);
        Map(0x26, "ROL", AddressingMode.ZeroPage, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Rol(value);
            cpu.WriteByte(address, value);
        }, 5);
        Map(0x28, "PLP", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._status = (StatusFlag)(cpu.PopByte() | (byte)StatusFlag.Unused);
            cpu.SetFlag(StatusFlag.Break, false);
        }, 4);
        Map(0x29, "AND", AddressingMode.Immediate, static (cpu, mode) => cpu.And(mode), 2);
        Map(0x2A, "ROL", AddressingMode.Accumulator, static (cpu, _) => cpu._a = cpu.Rol(cpu._a), 2);
        Map(0x2C, "BIT", AddressingMode.Absolute, static (cpu, mode) => cpu.Bit(mode), 4);
        Map(0x2D, "AND", AddressingMode.Absolute, static (cpu, mode) => cpu.And(mode), 4);
        Map(0x2E, "ROL", AddressingMode.Absolute, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Rol(value);
            cpu.WriteByte(address, value);
        }, 6);

        Map(0x30, "BMI", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(cpu.IsFlagSet(StatusFlag.Negative)), 2, branch: true);
        Map(0x31, "AND", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.And(mode), 5, page: true);
        Map(0x35, "AND", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.And(mode), 4);
        Map(0x36, "ROL", AddressingMode.ZeroPageX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Rol(value);
            cpu.WriteByte(address, value);
        }, 6);
        Map(0x38, "SEC", AddressingMode.Implicit, static (cpu, _) => cpu.Flag(StatusFlag.Carry, true), 2);
        Map(0x39, "AND", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.And(mode), 4, page: true);
        Map(0x3D, "AND", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.And(mode), 4, page: true);
        Map(0x3E, "ROL", AddressingMode.AbsoluteX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Rol(value);
            cpu.WriteByte(address, value);
        }, 7);

        Map(0x40, "RTI", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._status = (StatusFlag)(cpu.PopByte() | (byte)StatusFlag.Unused);
            cpu.SetFlag(StatusFlag.Break, false);
            cpu._pc = cpu.PopWord();
        }, 6);
        Map(0x41, "EOR", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.Eor(mode), 6);
        Map(0x45, "EOR", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Eor(mode), 3);
        Map(0x46, "LSR", AddressingMode.ZeroPage, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Lsr(value);
            cpu.WriteByte(address, value);
        }, 5);
        Map(0x48, "PHA", AddressingMode.Implicit, static (cpu, _) => cpu.PushByte(cpu._a), 3);
        Map(0x49, "EOR", AddressingMode.Immediate, static (cpu, mode) => cpu.Eor(mode), 2);
        Map(0x4A, "LSR", AddressingMode.Accumulator, static (cpu, _) => cpu._a = cpu.Lsr(cpu._a), 2);
        Map(0x4C, "JMP", AddressingMode.Absolute, static (cpu, _) => cpu._pc = cpu.FetchWord(), 3);
        Map(0x4D, "EOR", AddressingMode.Absolute, static (cpu, mode) => cpu.Eor(mode), 4);
        Map(0x4E, "LSR", AddressingMode.Absolute, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Lsr(value);
            cpu.WriteByte(address, value);
        }, 6);

        Map(0x50, "BVC", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(!cpu.IsFlagSet(StatusFlag.Overflow)), 2, branch: true);
        Map(0x51, "EOR", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.Eor(mode), 5, page: true);
        Map(0x55, "EOR", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Eor(mode), 4);
        Map(0x56, "LSR", AddressingMode.ZeroPageX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Lsr(value);
            cpu.WriteByte(address, value);
        }, 6);
        Map(0x58, "CLI", AddressingMode.Implicit, static (cpu, _) => cpu.Flag(StatusFlag.InterruptDisable, false), 2);
        Map(0x59, "EOR", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Eor(mode), 4, page: true);
        Map(0x5D, "EOR", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Eor(mode), 4, page: true);
        Map(0x5E, "LSR", AddressingMode.AbsoluteX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Lsr(value);
            cpu.WriteByte(address, value);
        }, 7);

        Map(0x60, "RTS", AddressingMode.Implicit, static (cpu, _) =>
        {
            var address = cpu.PopWord();
            cpu._pc = (ushort)(address + 1);
        }, 6);
        Map(0x61, "ADC", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.Adc(mode), 6);
        Map(0x65, "ADC", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Adc(mode), 3);
        Map(0x66, "ROR", AddressingMode.ZeroPage, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Ror(value);
            cpu.WriteByte(address, value);
        }, 5);
        Map(0x68, "PLA", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._a = cpu.PopByte();
            cpu.SetZeroAndNegative(cpu._a);
        }, 4);
        Map(0x69, "ADC", AddressingMode.Immediate, static (cpu, mode) => cpu.Adc(mode), 2);
        Map(0x6A, "ROR", AddressingMode.Accumulator, static (cpu, _) => cpu._a = cpu.Ror(cpu._a), 2);
        Map(0x6C, "JMP", AddressingMode.Indirect, static (cpu, _) =>
        {
            var pointer = cpu.FetchWord();
            cpu._pc = cpu.ReadWordBug(pointer);
        }, 5);
        Map(0x6D, "ADC", AddressingMode.Absolute, static (cpu, mode) => cpu.Adc(mode), 4);
        Map(0x6E, "ROR", AddressingMode.Absolute, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Ror(value);
            cpu.WriteByte(address, value);
        }, 6);

        Map(0x70, "BVS", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(cpu.IsFlagSet(StatusFlag.Overflow)), 2, branch: true);
        Map(0x71, "ADC", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.Adc(mode), 5, page: true);
        Map(0x75, "ADC", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Adc(mode), 4);
        Map(0x76, "ROR", AddressingMode.ZeroPageX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Ror(value);
            cpu.WriteByte(address, value);
        }, 6);
        Map(0x78, "SEI", AddressingMode.Implicit, static (cpu, _) => cpu.Flag(StatusFlag.InterruptDisable, true), 2);
        Map(0x79, "ADC", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Adc(mode), 4, page: true);
        Map(0x7D, "ADC", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Adc(mode), 4, page: true);
        Map(0x7E, "ROR", AddressingMode.AbsoluteX, static (cpu, mode) =>
        {
            var address = cpu.GetAddressForWrite(mode);
            var value = cpu.ReadByte(address);
            value = cpu.Ror(value);
            cpu.WriteByte(address, value);
        }, 7);

        Map(0x81, "STA", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.Store(mode, cpu._a), 6);
        Map(0x84, "STY", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Store(mode, cpu._y), 3);
        Map(0x85, "STA", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Store(mode, cpu._a), 3);
        Map(0x86, "STX", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Store(mode, cpu._x), 3);
        Map(0x88, "DEY", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._y--;
            cpu.SetZeroAndNegative(cpu._y);
        }, 2);
        Map(0x8A, "TXA", AddressingMode.Implicit, static (cpu, _) => cpu.Transfer(ref cpu._a, cpu._x), 2);
        Map(0x8C, "STY", AddressingMode.Absolute, static (cpu, mode) => cpu.Store(mode, cpu._y), 4);
        Map(0x8D, "STA", AddressingMode.Absolute, static (cpu, mode) => cpu.Store(mode, cpu._a), 4);
        Map(0x8E, "STX", AddressingMode.Absolute, static (cpu, mode) => cpu.Store(mode, cpu._x), 4);

        Map(0x90, "BCC", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(!cpu.IsFlagSet(StatusFlag.Carry)), 2, branch: true);
        Map(0x91, "STA", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.Store(mode, cpu._a), 6);
        Map(0x94, "STY", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Store(mode, cpu._y), 4);
        Map(0x95, "STA", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Store(mode, cpu._a), 4);
        Map(0x96, "STX", AddressingMode.ZeroPageY, static (cpu, mode) => cpu.Store(mode, cpu._x), 4);
        Map(0x98, "TYA", AddressingMode.Implicit, static (cpu, _) => cpu.Transfer(ref cpu._a, cpu._y), 2);
        Map(0x99, "STA", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Store(mode, cpu._a), 5);
        Map(0x9A, "TXS", AddressingMode.Implicit, static (cpu, _) => cpu._sp = cpu._x, 2);
        Map(0x9D, "STA", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Store(mode, cpu._a), 5);

        Map(0xA0, "LDY", AddressingMode.Immediate, static (cpu, mode) => cpu.Load(mode, ref cpu._y), 2);
        Map(0xA1, "LDA", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 6);
        Map(0xA2, "LDX", AddressingMode.Immediate, static (cpu, mode) => cpu.Load(mode, ref cpu._x), 2);
        Map(0xA4, "LDY", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Load(mode, ref cpu._y), 3);
        Map(0xA5, "LDA", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 3);
        Map(0xA6, "LDX", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Load(mode, ref cpu._x), 3);
        Map(0xA8, "TAY", AddressingMode.Implicit, static (cpu, _) => cpu.Transfer(ref cpu._y, cpu._a), 2);
        Map(0xA9, "LDA", AddressingMode.Immediate, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 2);
        Map(0xAA, "TAX", AddressingMode.Implicit, static (cpu, _) => cpu.Transfer(ref cpu._x, cpu._a), 2);
        Map(0xAC, "LDY", AddressingMode.Absolute, static (cpu, mode) => cpu.Load(mode, ref cpu._y), 4);
        Map(0xAD, "LDA", AddressingMode.Absolute, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 4);
        Map(0xAE, "LDX", AddressingMode.Absolute, static (cpu, mode) => cpu.Load(mode, ref cpu._x), 4);

        Map(0xB0, "BCS", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(cpu.IsFlagSet(StatusFlag.Carry)), 2, branch: true);
        Map(0xB1, "LDA", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 5, page: true);
        Map(0xB4, "LDY", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Load(mode, ref cpu._y), 4);
        Map(0xB5, "LDA", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 4);
        Map(0xB6, "LDX", AddressingMode.ZeroPageY, static (cpu, mode) => cpu.Load(mode, ref cpu._x), 4);
        Map(0xB8, "CLV", AddressingMode.Implicit, static (cpu, _) => cpu.Flag(StatusFlag.Overflow, false), 2);
        Map(0xB9, "LDA", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 4, page: true);
        Map(0xBA, "TSX", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._x = cpu._sp;
            cpu.SetZeroAndNegative(cpu._x);
        }, 2);
        Map(0xBC, "LDY", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Load(mode, ref cpu._y), 4, page: true);
        Map(0xBD, "LDA", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Load(mode, ref cpu._a), 4, page: true);
        Map(0xBE, "LDX", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Load(mode, ref cpu._x), 4, page: true);

        Map(0xC0, "CPY", AddressingMode.Immediate, static (cpu, mode) => cpu.Compare(mode, cpu._y), 2);
        Map(0xC1, "CMP", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.Compare(mode, cpu._a), 6);
        Map(0xC4, "CPY", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Compare(mode, cpu._y), 3);
        Map(0xC5, "CMP", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Compare(mode, cpu._a), 3);
        Map(0xC6, "DEC", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Dec(mode), 5);
        Map(0xC8, "INY", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._y++;
            cpu.SetZeroAndNegative(cpu._y);
        }, 2);
        Map(0xC9, "CMP", AddressingMode.Immediate, static (cpu, mode) => cpu.Compare(mode, cpu._a), 2);
        Map(0xCA, "DEX", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._x--;
            cpu.SetZeroAndNegative(cpu._x);
        }, 2);
        Map(0xCC, "CPY", AddressingMode.Absolute, static (cpu, mode) => cpu.Compare(mode, cpu._y), 4);
        Map(0xCD, "CMP", AddressingMode.Absolute, static (cpu, mode) => cpu.Compare(mode, cpu._a), 4);
        Map(0xCE, "DEC", AddressingMode.Absolute, static (cpu, mode) => cpu.Dec(mode), 6);

        Map(0xD0, "BNE", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(!cpu.IsFlagSet(StatusFlag.Zero)), 2, branch: true);
        Map(0xD1, "CMP", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.Compare(mode, cpu._a), 5, page: true);
        Map(0xD5, "CMP", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Compare(mode, cpu._a), 4);
        Map(0xD6, "DEC", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Dec(mode), 6);
        Map(0xD8, "CLD", AddressingMode.Implicit, static (cpu, _) => cpu.Flag(StatusFlag.Decimal, false), 2);
        Map(0xD9, "CMP", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Compare(mode, cpu._a), 4, page: true);
        Map(0xDD, "CMP", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Compare(mode, cpu._a), 4, page: true);
        Map(0xDE, "DEC", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Dec(mode), 7);

        Map(0xE0, "CPX", AddressingMode.Immediate, static (cpu, mode) => cpu.Compare(mode, cpu._x), 2);
        Map(0xE1, "SBC", AddressingMode.IndexedIndirect, static (cpu, mode) => cpu.Sbc(mode), 6);
        Map(0xE4, "CPX", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Compare(mode, cpu._x), 3);
        Map(0xE5, "SBC", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Sbc(mode), 3);
        Map(0xE6, "INC", AddressingMode.ZeroPage, static (cpu, mode) => cpu.Inc(mode), 5);
        Map(0xE8, "INX", AddressingMode.Implicit, static (cpu, _) =>
        {
            cpu._x++;
            cpu.SetZeroAndNegative(cpu._x);
        }, 2);
        Map(0xE9, "SBC", AddressingMode.Immediate, static (cpu, mode) => cpu.Sbc(mode), 2);
        Map(0xEA, "NOP", AddressingMode.Implicit, static (_, _) => { }, 2);
        Map(0xEC, "CPX", AddressingMode.Absolute, static (cpu, mode) => cpu.Compare(mode, cpu._x), 4);
        Map(0xED, "SBC", AddressingMode.Absolute, static (cpu, mode) => cpu.Sbc(mode), 4);
        Map(0xEE, "INC", AddressingMode.Absolute, static (cpu, mode) => cpu.Inc(mode), 6);

        Map(0xF0, "BEQ", AddressingMode.Relative, static (cpu, _) => cpu.BranchIf(cpu.IsFlagSet(StatusFlag.Zero)), 2, branch: true);
        Map(0xF1, "SBC", AddressingMode.IndirectIndexed, static (cpu, mode) => cpu.Sbc(mode), 5, page: true);
        Map(0xF5, "SBC", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Sbc(mode), 4);
        Map(0xF6, "INC", AddressingMode.ZeroPageX, static (cpu, mode) => cpu.Inc(mode), 6);
        Map(0xF8, "SED", AddressingMode.Implicit, static (cpu, _) => cpu.Flag(StatusFlag.Decimal, true), 2);
        Map(0xF9, "SBC", AddressingMode.AbsoluteY, static (cpu, mode) => cpu.Sbc(mode), 4, page: true);
        Map(0xFD, "SBC", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Sbc(mode), 4, page: true);
        Map(0xFE, "INC", AddressingMode.AbsoluteX, static (cpu, mode) => cpu.Inc(mode), 7);

        return table;
    }
}
