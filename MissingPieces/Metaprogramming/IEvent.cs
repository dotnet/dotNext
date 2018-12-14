using System.Reflection;

namespace MissingPieces.Metaprogramming
{
	internal interface IEvent : IMember<EventInfo>
	{
		bool CanAdd { get; }
		bool CanRemove { get; }
	}
}