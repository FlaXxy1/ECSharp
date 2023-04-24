namespace ECSharp;

internal class ArchetypeDataStore
{
	public const int CAPACITY = 4096;
	private readonly Array[] _componentStores;
	public readonly Archetype Archetype;

	public ArchetypeDataStore(Archetype archetype)
	{
		Archetype = archetype;
		_componentStores = Archetype.Types.Select(t => MakeStore(t, CAPACITY)).ToArray();
		Entities = new Entity[CAPACITY];
	}

	public int Count { get; private set; }
	public Entity?[] Entities { get; }

	private static Array MakeStore(int typeId, int capacity)
	{
		return Array.CreateInstance(TypeManager.FromId(typeId), capacity);
	}

	public void Remove(Entity entity)
	{
		var oldIndex = entity.Index;
		if (oldIndex >= Count || oldIndex < 0 || entity.Store != this || Count == 0)
			throw new Exception();


		if (oldIndex != Count - 1)
		{
			foreach (var componentStore in _componentStores)
				componentStore.SetValue(componentStore.GetValue(Count - 1), oldIndex);
			var lastEntity = Entities[Count - 1];
			Entities[oldIndex] = lastEntity;
			lastEntity!.Index = oldIndex;
		}
		else
		{
			foreach (var componentStore in _componentStores)
				Array.Clear(componentStore, oldIndex, 1);

			Entities[oldIndex] = null;
		}

		entity.Store = null;
		entity.Index = -1;
		Count--;
	}

	/// <summary>
	///     Allocates space for a new Entity.
	/// </summary>
	/// <returns></returns>
	public int Allocate()
	{
		if (Count >= CAPACITY)
			throw new ArgumentOutOfRangeException();

		return Count++;
	}

	/// <summary>
	///     Only Copies the Data of one entity to another Location.
	/// </summary>
	/// <param name="oldIndex"></param>
	/// <param name="newLocation"></param>
	/// <param name="newIndex"></param>
	public void CopyDataTo(int oldIndex, ArchetypeDataStore newLocation, int newIndex)
	{
		var oldComponentIndex = 0;
		var newComponentIndex = 0;
		//todo: check, that we are initializing all remaining fields in the new Store
		while (oldComponentIndex < _componentStores.Length || newComponentIndex < newLocation._componentStores.Length)
		{
			//the nullchecks are for the cases, where there are no items in one xor the other.
			var oldComponentType =
				oldComponentIndex < _componentStores.Length ? Archetype.Types[oldComponentIndex] : -1;
			var newComponentType = newComponentIndex < newLocation._componentStores.Length
				? newLocation.Archetype.Types[newComponentIndex]
				: -1;
			if (oldComponentType == newComponentType && oldComponentType != -1)
			{
				Array.Copy(
					_componentStores[oldComponentIndex],
					oldIndex,
					newLocation._componentStores[newComponentIndex],
					newIndex,
					1);
				oldComponentIndex++;
				newComponentIndex++;
				continue;
			}

			if (oldComponentType == -1 || oldComponentType >= newComponentType)
			{
				//Todo: does this need clearing, or will there never be garbage in the target?
				Array.Clear(newLocation._componentStores[newComponentIndex], newIndex, 1);
				newComponentIndex++;
				continue;
			}

			oldComponentIndex++;
		}
	}

	/// <summary>
	///     Links the Entity to this DataStore.
	/// </summary>
	/// <param name="ntt"></param>
	/// <param name="index"></param>
	public void InitializeForEntity(Entity ntt, int index)
	{
		Entities[index] = ntt;
		ntt.Store = this;
		ntt.Index = index;
	}

	public Array GetComponentStore(int typeId)
	{
		var index = Archetype.IndexOfType(typeId) ??
			throw new ArgumentException("No Store for type found", nameof(typeId));
		return _componentStores[index];
	}
}