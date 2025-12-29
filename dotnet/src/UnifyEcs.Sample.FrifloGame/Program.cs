using System;
using UnifyEcs.Sample.FrifloGame;
using Friflo.Engine.ECS;

// Simple test without source generator - direct Friflo API usage
var store = new EntityStore();

// Create entity directly with Friflo's API
var frifloEntity = store.CreateEntity();
var position = new SamplePosition { X = 0f, Y = 0f };
frifloEntity.AddComponent(position);

Console.WriteLine($"Created Friflo entity with ID: {frifloEntity.Id}");
Console.WriteLine($"Initial position: X={position.X}, Y={position.Y}");

for (int i = 0; i < 10; i++)
{
    // Update the position component directly
    ref var pos = ref frifloEntity.GetComponent<SamplePosition>();
    pos.X += 1f;
    Console.WriteLine($"After step {i + 1}: Position X={pos.X}");
}

Console.WriteLine("Test completed successfully!");
