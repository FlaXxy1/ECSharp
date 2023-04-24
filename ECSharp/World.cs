namespace ECSharp;

public class World
{
	private readonly Dictionary<ArchetypeDescription, Archetype> _archetypes = new();
	private readonly List<Entity> _entities = new();
	private readonly Archetype _identity = new(Span<int>.Empty);
	private readonly Dictionary<IQuery, QueryDesc> _queries = new();

	public World()
	{
		_archetypes[_identity] = _identity;
	}

	public IReadOnlyList<IEntity> Entities => _entities;

	public ref T AddComponent<T>(IEntity entity)
	{
		var e = Get(entity);

		var target = GetArchetypeWithAddedComponent(e.Store?.Archetype ?? _identity, TypeId<T>.Id);

		MoveEntityToArchetype(e, target);
		var store = (T[])e.Store!.GetComponentStore(TypeId<T>.Id);
		return ref store[e.Index];
	}

	public void RemoveComponent<T>(IEntity entity)
	{
		var e = Get(entity);

		var target = GetArchetypeWithRemovedComponent(e.Store?.Archetype ?? _identity, TypeId<T>.Id);

		MoveEntityToArchetype(e, target);
	}

	public IEntity CreateEntity()
	{
		var (newLocation, newIndex) = _identity.AllocateNewStoreLocation();
		var entity = new Entity();
		newLocation.InitializeForEntity(entity, newIndex);
		_entities.Add(entity);
		return entity;
	}


	private unsafe Archetype GetArchetypeWithRemovedComponent(Archetype at, int oldType)
	{
		var size = at.Types.Count - 1;
		var types = stackalloc int[size];

		var removed = false;
		for (var targetIndex = 0; targetIndex < size; ++targetIndex)
			if (removed)
			{
				types[targetIndex] = at.Types[targetIndex + 1];
			}
			else if (at.Types[targetIndex] == oldType)
			{
				removed = true;
				types[targetIndex] = at.Types[targetIndex + 1];
			}
			else if (at.Types[targetIndex] < oldType)
			{
				types[targetIndex] = at.Types[targetIndex];
			}

		if (!removed)
			//it's the same Archetype.
			return at;

		return GetOrAddArchetype(types, size);
	}

	private unsafe Archetype GetArchetypeWithAddedComponent(Archetype at, int newType)
	{
		var size = at.Types.Count + 1;
		var types = stackalloc int[size];

		var inserted = false;
		for (var i = 0; i < size; ++i)
			if (inserted)
			{
				types[i] = at.Types[i - 1];
			}
			else if (i >= at.Types.Count || at.Types[i] > newType)
			{
				inserted = true;
				types[i] = newType;
			}
			else if (at.Types[i] < newType)
			{
				types[i] = at.Types[i];
			}
			else
			{
				//it's the same archetype.
				return at;
			}

		return GetOrAddArchetype(types, size);
	}

	private unsafe Archetype GetOrAddArchetype(int* types, int size)
	{
		if (_archetypes.TryGetValue(new ArchetypeDescription(types, size), out var archetype))
			return archetype;

		archetype = new Archetype(new Span<int>(types, size));
		_archetypes.Add(archetype, archetype);
		foreach (var (query, desc) in _queries)
			if (desc.Matches(archetype))
				archetype.RegisterQuery(query);

		return archetype;
	}


    /// <summary>
    ///     Can only move alive Entities, that are already in a store.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="target"></param>
    /// <exception cref="Exception"></exception>
    private void MoveEntityToArchetype(Entity entity, Archetype? target)
	{
		if (entity.Store?.Archetype == target)
			return;
		var oldLocation = entity.Store ?? throw new Exception();
		if (target == null)
		{
			oldLocation.Remove(entity);
			return;
		}

		var (newLocation, newIndex) = target.AllocateNewStoreLocation();
		oldLocation.CopyDataTo(entity.Index, newLocation, newIndex);
		oldLocation.Remove(entity);

		//maybe optimize here.
		//todo: compact sometimes?

		newLocation.InitializeForEntity(entity, newIndex);
	}

	public void RemoveEmptyStores()
	{
		foreach (var archetypes in _archetypes.Values)
			archetypes.RemoveEmptyStores();
	}


	private static Entity Get(IEntity entity)
	{
		if (entity is not Entity e || e.IsDestroyed || e.Store == null)
			throw new Exception();
		return e;
	}

    /// <summary>
    ///     Every query can only be registered once. When registering, you get a callback for every store.
    /// </summary>
    /// <param name="desc"></param>
    /// <param name="query"></param>
    internal void RegisterQuery(IQuery query, in QueryDesc desc)
	{
		_queries.Add(query, desc);
		foreach (var archetype in _archetypes.Values)
			if (desc.Matches(archetype))
				archetype.RegisterQuery(query);
	}

	internal void DeregisterQuery(IQuery query)
	{
		_queries.Remove(query);
		foreach (var archetype in _archetypes.Values)
			archetype.DeregisterQuery(query);
	}

	public void Destroy(IEntity entity)
	{
		var e = Get(entity);
		MoveEntityToArchetype(e, null);
		_entities.Remove(e);
		e.IsDestroyed = true;
	}
}

internal class Entity : IEntity
{
	public bool IsDestroyed { get; set; }
	public ArchetypeDataStore? Store { get; set; }
	public int Index { get; set; } = -1;
}

public interface IEntity
{
}