using System;
using UnifyECS;

namespace UnifyEcs.Sample.FlecsGame;

/// <summary>
/// Sample game demonstrating UnifyECS with Flecs.NET backend.
/// </summary>
public static class Program
{
    public static void Main()
    {
        Console.WriteLine("=== UnifyECS Flecs Sample ===\n");

        // Create Flecs system runner
        using var runner = FlecsWorldFactory.Create(new UnifyECS.WorldConfig());
        var world = runner.World;

        Console.WriteLine("Creating entities...");

        // Create some test entities
        for (int i = 0; i < 5; i++)
        {
            var entity = world.CreateEntity(
                new Position(0, 0),
                new Velocity((i + 1) * 0.1f, (i + 1) * 0.05f),
                new Health(10 + i, 10 + i)
            );
            Console.WriteLine($"Created entity {entity.Id} with Position, Velocity, and Health components");
        }

        Console.WriteLine($"\nInitial entity count: {world.EntityCount}\n");

        // Note: Systems not yet generated - basic infrastructure in place
        Console.WriteLine("Flecs backend infrastructure complete.");
        Console.WriteLine("System generation will be added in future implementation.");

        Console.WriteLine("=== Simulation Complete ===");
    }
}
