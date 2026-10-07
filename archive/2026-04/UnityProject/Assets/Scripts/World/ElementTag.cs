using System;

namespace ZeldaDaughter.World
{
    [Flags]
    public enum ElementTag
    {
        None        = 0,
        Fire        = 1 << 0,
        Wet         = 1 << 1,
        Electrified = 1 << 2,
        Muddy       = 1 << 3
    }
}
