using System;
using UnifyECS;
using UnifyEcs.Sample.ArchGame;

// Create an Arch-backed world via the unified factory
var runner = new ArchMultiWorldRunner();

// Create a runner bound to this world
var gameWorld = runner.Worlds.GetOrCreate("Game");
var uiWorld = runner.Worlds.GetOrCreate("UI");

// Create an entity with a Position component
var entity = gameWorld.CreateEntity();
ref var position = ref gameWorld.Add(entity, new Position { X = 0f, Y = 0f });
ref var perception = ref gameWorld.Add(entity, new Perception { VisibleCount = 0 });

// Create a command buffer for deferred structural changes
using var commands = new DefaultCommandBuffer();

// Register the user-defined systems (generator will provide the Arch backend)
var moveRight = new MoveRightSystem();
var spawn = new SpawnSystem { Commands = commands };
var killIfTooFar = new KillIfTooFarSystem { World = gameWorld };
var visualPerception = new VisualPerceptionSystem();

runner.Register("Game", moveRight);
runner.Register("Game", spawn);
runner.Register("Game", killIfTooFar);
runner.Register("Game", visualPerception);

runner.Initialize();

Console.WriteLine($"Before update: X={position.X}, Y={position.Y}, Visible={perception.VisibleCount}, GameEntities={gameWorld.EntityCount}, UiEntities={uiWorld.EntityCount}");
const int steps = 120;
for (var i = 1; i <= steps; i++)
{
    runner.Update(1f / 60f);
    Console.WriteLine($"After  update {i}: X={position.X}, Y={position.Y}, Visible={perception.VisibleCount}, GameEntities={gameWorld.EntityCount}, UiEntities={uiWorld.EntityCount}");
    if (gameWorld.EntityCount == 0)
    {
        break;
    }
}

runner.Dispose();
gameWorld.Dispose();
uiWorld.Dispose();
