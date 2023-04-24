namespace ECSharp;

public record struct ComponentAccessDesc(int TypeId, bool IsReadonly)
{
	public static ComponentAccessDesc CreateReadonly<T>()
	{
		return new ComponentAccessDesc(TypeId<T>.Id, true);
	}

	public static ComponentAccessDesc Create<T>()
	{
		return new ComponentAccessDesc(TypeId<T>.Id, false);
	}
}

/// <summary>
///     Do a query description like this (and not a predicate), so that we might retain the Queries and cache the
///     Archetypes, that belong to this query.
/// </summary>
/// <param name="All"></param>
public record struct QueryDesc(ComponentAccessDesc[] All)
{
	internal readonly bool Matches(Archetype at)
	{
		return All.All(t => at.Types.Contains(t.TypeId));
	}
}

public struct ReadAccess<TComponent> : IComponentAccess<TComponent>
{
	public readonly ref readonly TComponent Get()
	{
		return ref _data[_index];
	}

	private int _index;
	private TComponent[] _data;

	void IComponentAccess<TComponent>.Init(int index, TComponent[] data)
	{
		_index = index;
		_data = data;
	}

	ComponentAccessDesc IComponentAccess<TComponent>.GetComponentAccess()
	{
		return ComponentAccessDesc.CreateReadonly<TComponent>();
	}
}

public struct WriteAcces<TComponent> : IComponentAccess<TComponent>
{
	public readonly ref TComponent Get()
	{
		return ref _data[_index];
	}

	private int _index;
	private TComponent[] _data;

	void IComponentAccess<TComponent>.Init(int index, TComponent[] data)
	{
		_index = index;
		_data = data;
	}

	ComponentAccessDesc IComponentAccess<TComponent>.GetComponentAccess()
	{
		return ComponentAccessDesc.Create<TComponent>();
	}
}

internal interface IQuery
{
	void OnStoreAdded(ArchetypeDataStore store);

	void OnStoreRemoved(ArchetypeDataStore store);
}

public interface IComponentAccess<in T>
{
	internal void Init(int index, T[] data);
	internal ComponentAccessDesc GetComponentAccess();
}