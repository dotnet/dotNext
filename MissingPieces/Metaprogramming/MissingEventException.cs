using System;

namespace MissingPieces.Metaprogramming
{
	public sealed class MissingEventException : ConstraintException
	{
		private MissingEventException(Type declaringType,
			string eventName,
			Type handlerType)
			: base($"Event {eventName} of type {handlerType.FullName} doesn't exist in type {declaringType.FullName}", declaringType)
		{
			HandlerType = handlerType;
			EventName = eventName;
		}

		internal static MissingEventException Create<T, E>(string eventName)
			where E: MulticastDelegate
			=> new MissingEventException(typeof(T), eventName, typeof(E));

		public Type HandlerType { get; }
		public string EventName { get; }
	}
}
