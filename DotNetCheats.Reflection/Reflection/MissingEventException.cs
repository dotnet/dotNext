using System;

namespace Cheats.Reflection
{
	/// <summary>
	/// Indicates that requested event doesn't exist.
	/// </summary>
	public sealed class MissingEventException : ConstraintViolationException
	{
		private MissingEventException(Type declaringType, string eventName, Type handlerType)
			: base(declaringType, $"Event {eventName} of type {handlerType.FullName} doesn't exist in type {declaringType.FullName}")
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
