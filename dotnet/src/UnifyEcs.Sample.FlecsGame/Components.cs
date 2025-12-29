using UnifyECS;

namespace UnifyEcs.Sample.FlecsGame;

/// <summary>
/// Position component.
/// </summary>
[EcsComponent]
public struct Position
{
    public float X;
    public float Y;

    public Position(float x, float y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Velocity component.
/// </summary>
[EcsComponent]
public struct Velocity
{
    public float X;
    public float Y;

    public Velocity(float x, float y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Health component.
/// </summary>
[EcsComponent]
public struct Health
{
    public int Value;
    public int Max;

    public Health(int value, int max)
    {
        Value = value;
        Max = max;
    }
}
