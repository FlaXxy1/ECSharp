﻿using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SourceGen;

[Generator]
public class Generator : ISourceGenerator
{
	private const int MAX_ARGC = 8;

	private const string NAMESPACE = "ECSharp.Generated";

	public void Initialize(GeneratorInitializationContext context)
	{
	}

	public void Execute(GeneratorExecutionContext context)
	{
		var sb = new StringBuilder();
		sb.AppendLine(
			@"//Autogenerated by a SourceGen
#nullable enable");
		sb.AppendLine("using System.Collections;");
		sb.AppendLine($"namespace {NAMESPACE};");
		sb.AppendLine();
		for (var i = 1; i <= MAX_ARGC; i++)
			CreateQuery(i, sb);

		context.AddSource(
			"QueryImplementations.g.cs",
			SourceText.From(sb.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine), Encoding.UTF8));
		sb.Clear();
	}

	private static void CreateQuery(int argc, StringBuilder sb)
	{
		var numbers = Enumerable.Range(0, argc).ToArray();

		string Join(Func<int, string> func, string separator = "\n")
		{
			return string.Join(separator, numbers.Select(func));
		}

		var typeDefs = Join(i => $"T{i}, TA{i}", ", ");
		var accessTypeDefs = Join(i => $"TA{i}", ", ");
		var typeConstraints = Join(i => $"    where TA{i} : struct, IComponentAccess<T{i}>");
		var storeInits = Join(i => $"_t{i} = (T{i}[])store.GetComponentStore(TypeId<T{i}>.Id);");
		var accessInits = Join(
			i => @$"            var ta{i} = new TA{i}();
            ta{i}.Init(index, _t{i});");
		var componentAccesses = string.Join(",\n", numbers.Select(n => $"new TA{n}().GetComponentAccess()"));

		var line =
			$@"
public readonly struct Batch<{typeDefs}>
{typeConstraints}
{{
{Join(i => $"public readonly T{i}[] _t{i};")}
    internal readonly ArchetypeDataStore Store;
    public int Count => Store.Count;

    internal Batch(ArchetypeDataStore store)
    {{
        Store = store;
{storeInits}
    }}

    public ({accessTypeDefs}, IEntity) this[int index]
    {{
        get
        {{
{accessInits}
            return ({Join(i => $"ta{i}", ", ")}, Store.Entities[index]!);
        }}
    }}
}}


/// <summary>
/// Query to iterate over a set of Entities with given components.
/// </summary>
/// <remarks>The Enumerable Implementation is probably slow, but easy. Use the batches directly for potentially more performance.</remarks>
public class Query<{typeDefs}> : IQuery, IDisposable, IEnumerable<({accessTypeDefs}, IEntity)>
{typeConstraints}
{{
    public Query(World world, QueryDesc? description = null)
    {{
        _world = world;
        if(description is not {{}} d)
        {{
            d = new QueryDesc
            {{
                All = new[]
                {{
{componentAccesses}
                }}
            }};
        }}

        _world.RegisterQuery(this, in d);
    }}

    private List<Batch<{typeDefs}>> _batches = new();
    private readonly World _world;
    public IReadOnlyList<Batch<{typeDefs}>> Batches => _batches;

    void IQuery.OnStoreAdded(ArchetypeDataStore store)
    {{
        _batches.Add(new (store));
    }}

    void IQuery.OnStoreRemoved(ArchetypeDataStore store)
    {{
        _batches.RemoveAll(b => b.Store == store);
    }}

    
    public IEnumerator<({accessTypeDefs}, IEntity)> GetEnumerator()
    {{
        return _batches.SelectMany(b => Enumerable.Range(0,b.Count).Select(i => b[i])).GetEnumerator();
    }}

    IEnumerator IEnumerable.GetEnumerator()
    {{
        return GetEnumerator();
    }}

    public void Dispose()
    {{
        //null batches should indicate, that it's disposed
        _batches = null!;
        _world.DeregisterQuery(this);
    }}
}}

";

		sb.AppendLine(line);
	}
}