using System.Diagnostics;
using System.Numerics;
using ECSharp;
using ECSharp.Generated;
using Microsoft.Toolkit.HighPerformance.Helpers;
using Raylib_cs;

var world = new World();
var rand = new Random();

Vector2 RandVec(float xRange, float yRange)
{
	return new Vector2(rand.NextSingle() * xRange, rand.NextSingle() * yRange);
}

float RandRot()
{
	return MathF.PI * 2 * rand.NextSingle();
}

//test initializing of Typeids in wrong order
_ = TypeId<LinVel>.Id;
_ = TypeId<GarbageComp>.Id;
_ = TypeId<Transform>.Id;

const int nttCount = 10000;
for (var i = 0; i < nttCount; i++)
{
	var ntt = world.CreateEntity();
	world.AddComponent<Transform>(ntt) = new Transform(RandVec(800, 600), RandRot());
	world.AddComponent<LinVel>(ntt) = new LinVel(RandVec(100, 100) - Vector2.One * 50);
	world.AddComponent<RenderComp>(ntt) = new RenderComp(1, Color.BLUE);
	if (rand.Next() % 2 == 0)
		world.AddComponent<GarbageComp>(ntt) = new GarbageComp(34, rand.NextSingle().ToString(), new object());
	if (rand.Next(1000) == 0)
		world.RemoveComponent<LinVel>(ntt);
}

foreach (var ntt in world.Entities.ToList())
	if (rand.Next(100) == 0)
		world.Destroy(ntt);

world.RemoveEmptyStores();
var dt = 1 / 60f;
Test2();

void Test2()
{
	Raylib.SetTargetFPS(60);
	Raylib.InitWindow(1000, 600, "App");
	using var movement = new Query<Transform, WriteAcces<Transform>, LinVel, ReadAccess<LinVel>>(world);
	using var rendering = new Query<Transform, ReadAccess<Transform>, RenderComp, ReadAccess<RenderComp>>(world);
	while (!Raylib.WindowShouldClose())
	{
		ParallelHelper.For(
			..movement.Batches.Count,
			new Movement
			{
				Batches = movement.Batches,
				Dt = dt
			});
		//Parallel.ForEach(movement.Batches, new ParallelOptions { MaxDegreeOfParallelism = 4 }, b =>
		//{
		//    for (var i = 0; i < b.Count; i++)
		//        //var (t, v, ntt) = b[i];
		//        //ref var transform = ref t.Get();
		//        //ref readonly var vel = ref v.Get();
		//        //transform.Position += dt * vel.Velocity;

		//        b._t0[i].Position += dt * b._t1[i].Velocity;
		//});
		Raylib.BeginDrawing();
		Raylib.ClearBackground(Color.RAYWHITE);
		foreach (var b in rendering.Batches)
			for (var i = 0; i < b.Count; i++)
			{
				var t = b._t0[i];
				var r = b._t1[i];
				Raylib.DrawCircle((int)t.Position.X, (int)t.Position.Y, r.radius, r.Color);
			}

		Raylib.EndDrawing();
	}
}

void Test1()
{
	var sw = Stopwatch.StartNew();
	const int iterations = 1000;
	using var movement = new Query<Transform, WriteAcces<Transform>, LinVel, ReadAccess<LinVel>>(world);
	for (var i = 0; i < iterations; i++)
		//var toremove = new ConcurrentBag<IEntity>();
		ParallelHelper.For(
			..movement.Batches.Count,
			new Movement
			{
				Batches = movement.Batches,
				Dt = dt
			});
	//Parallel.ForEach(movement.Batches, new ParallelOptions { MaxDegreeOfParallelism = -1 }, b =>
	//{
	//    for (var i = 0; i < b.Count; i++)
	//        //var (t, v, ntt) = b[i];
	//        //ref var transform = ref t.Get();
	//        //ref readonly var vel = ref v.Get();
	//        //transform.Position += dt * vel.Velocity;
	//        b._t0[i].Position += dt * b._t1[i].Velocity;
	//});
	Console.WriteLine(
		(sw.ElapsedTicks / (double)Stopwatch.Frequency / iterations).ToString("0." + new string('#', 339)));
}


public static class Helper
{
	public static void DestroyRandomEntities(World world, float survivalChance)
	{
		var rand = new Random();
		foreach (var entity in world.Entities.ToList())
			if (rand.NextSingle() > survivalChance)
				world.Destroy(entity);
	}
}


internal readonly struct Movement : IAction
{
	public IReadOnlyList<Batch<Transform, WriteAcces<Transform>, LinVel, ReadAccess<LinVel>>> Batches { get; init; }
	public float Dt { get; init; }

	public void Invoke(int b_i)
	{
		var batch = Batches[b_i];
		for (var i = 0; i < batch.Count; i++)
			batch._t0[i].Position += Dt * batch._t1[i].Velocity;
	}
}

internal record struct RenderComp(float radius, Color Color);

internal record struct GarbageComp(int schminnt, string Schming, object Oh);

internal record struct LinVel(Vector2 Velocity);

internal record struct Transform(Vector2 Position, float Orientation);