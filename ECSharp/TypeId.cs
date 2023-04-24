namespace ECSharp;

public static class TypeManager
{
	private static readonly List<Type> _types = new();

	internal static int Register(Type t)
	{
		lock (_types)
		{
			var count = _types.Count;
			_types.Add(t);
			return count;
		}
	}

	public static Type FromId(int id)
	{
		return _types[id];
	}
}

public class TypeId<TType>
{
	public static readonly int Id = TypeManager.Register(typeof(TType));
}