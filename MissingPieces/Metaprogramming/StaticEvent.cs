using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Provides typed access to static event.
	/// </summary>
	/// <typeparam name="T">Declaring type of event.</typeparam>
	/// <typeparam name="P">Event handler type.</typeparam>
	public readonly struct StaticEvent<T, E> : IEvent, IEquatable<StaticEvent<T, E>>, IEquatable<EventInfo>
		where E : MulticastDelegate
	{
		private sealed class Cache : MemberCache<EventInfo, StaticEvent<T, E>>
		{
			private protected override StaticEvent<T, E> CreateMember(string eventName)
				=> new StaticEvent<T, E>(eventName);
		}

		private static readonly Cache cache = new Cache();
		private readonly EventInfo @event;
		private readonly Action<E> addHandler;
		private readonly Action<E> removeHandler;

		private StaticEvent(string eventName)
		{
			@event = typeof(T).GetEvent(eventName, BindingFlags.Static | BindingFlags.Public);
			if (@event == null)
				addHandler = removeHandler = null;
			else
			{
				var handlerParam = Expression.Parameter(@event.EventHandlerType);
				addHandler = @event.AddMethod?.CreateDelegate<Action<E>>(null);
				removeHandler = @event.RemoveMethod?.CreateDelegate<Action<E>>(null);
			}
		}

		/// <summary>
		/// Gets name of this event.
		/// </summary>
		public string Name => @event.Name;

		/// <summary>
		/// Add event handler.
		/// </summary>
		/// <param name="handler">An event handler to add.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddHandler(E handler)
			=> addHandler(handler);

		/// <summary>
		/// Remove event handler.
		/// </summary>
		/// <param name="handler">An event handler to remove.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RemoveHandler(E handler)
			=> removeHandler(handler);

		EventInfo IMember<EventInfo>.Member => @event;

		/// <summary>
		/// Indicates that caller code can attach event handler.
		/// </summary>
		public bool CanAdd => addHandler != null;

		/// <summary>
		/// Indicates that caller code can detach event handler.
		/// </summary>
		public bool CanRemove => removeHandler != null;

		/// <summary>
		/// Indicates that this object references event.
		/// </summary>
		public bool IsPresent => @event != null;

		public bool Equals(EventInfo other) => @event == other;

		public bool Equals(in StaticEvent<T, E> other)
			=> Equals(other.@event);

		bool IEquatable<StaticEvent<T, E>>.Equals(StaticEvent<T, E> other)
			=> Equals(in other);

		public override bool Equals(object other)
		{
			switch (other)
			{
				case StaticEvent<T, E> @event:
					return Equals(in @event);
				case EventInfo @event:
					return Equals(@event);
				default:
					return false;
			}
		}

		public override int GetHashCode() => @event.GetHashCode();

		public override string ToString() => @event.ToString();

		public static bool operator ==(in StaticEvent<T, E> first, in StaticEvent<T, E> second)
			=> first.Equals(in second);

		public static bool operator !=(in StaticEvent<T, E> first, in StaticEvent<T, E> second)
			=> !first.Equals(in second);

		public static implicit operator EventInfo(in StaticEvent<T, E> @event)
			=> @event.@event;

		internal static StaticEvent<T, E> Get(string eventName)
			=> cache.GetOrCreate(eventName);
	}
}
