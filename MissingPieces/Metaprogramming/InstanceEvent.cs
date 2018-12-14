using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Provides typed access to instance event.
	/// </summary>
	/// <remarks>Access to event is organized through managed reference
	/// to the instance object. Therefore, original struct value will be modified.
	/// </remarks>
	/// <typeparam name="T">Declaring type of event.</typeparam>
	/// <typeparam name="E">Event handler type.</typeparam>
	public readonly struct InstanceEvent<T, E> : IEvent, IEquatable<InstanceEvent<T, E>>, IEquatable<EventInfo>
		where E : MulticastDelegate
	{
		private delegate void AddOrRemove(in T instance, E handler);

		private sealed class Cache : MemberCache<EventInfo, InstanceEvent<T, E>>
		{
			private protected override InstanceEvent<T, E> CreateMember(string eventName)
				=> new InstanceEvent<T, E>(eventName);
		}

		private static readonly Cache cache = new Cache();
		private readonly EventInfo @event;
		private readonly AddOrRemove addHandler;
		private readonly AddOrRemove removeHandler;

		private InstanceEvent(string eventName)
		{
			@event = typeof(T).GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
			if (@event is null || @event.EventHandlerType != typeof(E))
				addHandler = removeHandler = null;
			else
			{
				var instanceParam = Expression.Parameter(@event.DeclaringType.MakeByRefType());
				var handlerParam = Expression.Parameter(@event.EventHandlerType);
				addHandler = @event.AddMethod is null ?
					null :
					Expression.Lambda<AddOrRemove>(Expression.Invoke(instanceParam, handlerParam)).Compile();
				removeHandler = @event.RemoveMethod is null ?
					null :
					Expression.Lambda<AddOrRemove>(Expression.Invoke(instanceParam, handlerParam)).Compile();
			}
		}

		/// <summary>
		/// Gets name of this event.
		/// </summary>
		public string Name => @event.Name;

		/// <summary>
		/// Add event handler.
		/// </summary>
		/// <param name="instance">Object with declared event.</param>
		/// <param name="handler">An event handler to add.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddHandler(in T instance, E handler)
			=> addHandler(in instance, handler);

		/// <summary>
		/// Remove event handler.
		/// </summary>
		/// <param name="instance">Object with declared event.</param>
		/// <param name="handler">An event handler to remove.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RemoveHandler(in T instance, E handler)
			=> removeHandler(in instance, handler);

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
		public bool Exists => @event != null;

		public bool Equals(EventInfo other) => @event == other;

		public bool Equals(in InstanceEvent<T, E> other)
			=> Equals(other.@event);

		bool IEquatable<InstanceEvent<T, E>>.Equals(InstanceEvent<T, E> other)
			=> Equals(in other);

		public override bool Equals(object other)
		{
			switch (other)
			{
				case InstanceEvent<T, E> @event:
					return Equals(in @event);
				case EventInfo @event:
					return Equals(@event);
				default:
					return false;
			}
		}

		public override int GetHashCode() => @event.GetHashCode();

		public override string ToString() => @event.ToString();

		public static bool operator ==(in InstanceEvent<T, E> first, in InstanceEvent<T, E> second)
			=> first.Equals(in second);

		public static bool operator !=(in InstanceEvent<T, E> first, in InstanceEvent<T, E> second)
			=> !first.Equals(in second);

		public static implicit operator EventInfo(in InstanceEvent<T, E> @event)
			=> @event.@event;

		internal static InstanceEvent<T, E> Get(string eventName)
			=> cache.GetOrCreate(eventName);
	}
}
