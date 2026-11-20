using System;
// Copied from Kirby RPG
[Flags]
public enum OutlineCategory
{
    None = 0,
    Pokemon = 1 << 0,
    Human = 1 << 1,
    Object = 1 << 2,
    All = Pokemon | Human | Object,
}
