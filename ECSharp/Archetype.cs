namespace ECSharp;

internal readonly unsafe struct ArchetypeDescription : IEquatable<ArchetypeDescription>
{
	public bool Equals(ArchetypeDescription other)
	{
		if (other.Size != Size)
			return false;
		for (var i = 0; i < Size; i++)
			if (Get(i) != other.Get(i))
				return false;
		return true;
	}

	public override bool Equals(object? obj)
	{
		return obj is ArchetypeDescription other && Equals(other);
	}

	public override int GetHashCode()
	{
		return _hash;
	}

	public static bool operator ==(ArchetypeDescription left, ArchetypeDescription right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(ArchetypeDescription left, ArchetypeDescription right)
	{
		return !left.Equals(right);
	}

	private readonly bool _ownsMemory;
	private readonly int _hash;
	private readonly Archetype _memory;

	private readonly int size;
	private readonly int* data;


	public ArchetypeDescription(int* ptr, int size)
	{
		_ownsMemory = false;
		this.size = size;
		data = ptr;
		_memory = default!;
		_hash = CalcHash(new Span<int>(ptr, size));
	}

	private static int CalcHash(Span<int> data)
	{
		var hash = 0;
		for (var i = 0; i < data.Length; i++)
			hash = HashCode.Combine(hash, data[i]);
		return hash;
	}

	public ArchetypeDescription(Archetype memory)
	{
		_memory = memory;
		_ownsMemory = true;
		data = default;
		size = -1;
		_hash = memory.GetHashCode();
	}

	public static implicit operator ArchetypeDescription(Archetype at)
	{
		return new ArchetypeDescription(at);
	}

	public int Size => _ownsMemory ? _memory.Types.Count : size;

	public int Get(int index)
	{
		return _ownsMemory ? _memory.Types[index] : data[index];
	}
}

internal class Archetype
{
	private readonly List<ArchetypeDataStore> _dataStores = new();
	private readonly int _hash;
	private readonly HashSet<IQuery> _queries = new();
	private readonly int[] _types;

    /// <summary>
    ///     needs to be presorted.
    /// </summary>
    /// <param name="types"></param>
    public Archetype(Span<int> types)
	{
		_types = types.ToArray();
		foreach (var type in _types)
			_hash = HashCode.Combine(_hash, type.GetHashCode());
	}

	public IReadOnlyList<int> Types => _types;

	public int? IndexOfType(int typeId)
	{
		for (var i = 0; i < _types.Length; i++)
			if (typeId == _types[i])
				return i;
		return null;
	}

	protected bool Equals(Archetype other)
	{
		return _types.SequenceEqual(other._types);
	}

	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(null, obj))
			return false;
		if (ReferenceEquals(this, obj))
			return true;
		if (obj.GetType() != GetType())
			return false;
		return Equals((Archetype)obj);
	}

	public override int GetHashCode()
	{
		return _hash;
	}

	public static bool operator ==(Archetype? left, Archetype? right)
	{
		return Equals(left, right);
	}

	public static bool operator !=(Archetype? left, Archetype? right)
	{
		return !Equals(left, right);
	}

	public override string ToString()
	{
		return string.Join(",", _types.Select(t => $"{{{TypeManager.FromId(t).FullName}}}"));
	}

	public (ArchetypeDataStore store, int index) AllocateNewStoreLocation()
	{
		ArchetypeDataStore? store = null;

		for (var i = 0; i < _dataStores.Count; i++)
			if (_dataStores[i].Count < ArchetypeDataStore.CAPACITY)
			{
				store = _dataStores[i];
				break;
			}

		store ??= AddStore();

		return (store, store.Allocate());
	}

	private ArchetypeDataStore AddStore()
	{
		var store = new ArchetypeDataStore(this);
		_dataStores.Add(store);
		foreach (var query in _queries)
			query.OnStoreAdded(store);

		return store;
	}

	private void RemoveStore(ArchetypeDataStore store)
	{
		_dataStores.Remove(store);
		foreach (var query in _queries)
			query.OnStoreRemoved(store);
	}

	public void RemoveEmptyStores()
	{
		foreach (var store in _dataStores.ToList())
			if (store.Count == 0)
				RemoveStore(store);
	}

	public void RegisterQuery(IQuery query)
	{
		_queries.Add(query);
		foreach (var store in _dataStores)
			query.OnStoreAdded(store);
	}

	public void DeregisterQuery(IQuery query)
	{
		_queries.Remove(query);
	}
}