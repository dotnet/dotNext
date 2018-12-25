using System;
using System.Linq.Expressions;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Represents instance event subscribers.
	/// </summary>
	/// <typeparam name="T">Event declaring type.</typeparam>
	/// <typeparam name="H">Type of event handler.</typeparam>
	/// <param name="instance">An instance with event.</param>
	/// <param name="handler">Handler to attach/detach to/from event.</param>
	/// <param name="action">Performed action.</param>
	/// <returns>True, if action is supported by member; otherwise, false.</returns>
	public delegate bool EventAccess<T, in H>(in T instance, H handler, EventAction action)
		where H : MulticastDelegate;

	/// <summary>
	/// Represents static event subscribers.
	/// </summary>
	/// <typeparam name="H">Type of event handler.</typeparam>
	/// <param name="handler">Handler to attach/detach to/from event.</param>
	/// <param name="action">Performed action.</param>
	/// <returns>True, if action is supported by member; otherwise, false.</returns>
	public delegate bool EventAccess<in H>(H handler, EventAction action);

	public static class EventAccess
	{
		private static readonly ConstantExpression AddHandlerConst = Expression.Constant(EventAction.AddHandler, typeof(EventAction));

		internal static ConditionalExpression AddOrRemoveHandler(Expression action, Expression addHandler, Expression removeHandler)
		{
			addHandler = addHandler is null ?
				Expression.Constant(false).Upcast<Expression, ConstantExpression>() :
				Expression.Block(addHandler, Expression.Constant(true));
			removeHandler = removeHandler is null ?
				Expression.Constant(false).Upcast<Expression, ConstantExpression>() :
				Expression.Block(removeHandler, Expression.Constant(true));
			return Expression.Condition(Expression.Equal(action, AddHandlerConst), addHandler, removeHandler);
		}

		private static MemberAccessException CannotAddHandler() => new MemberAccessException("Event subscription is not supported");
		private static MemberAccessException CannotRemoveHandler() => new MemberAccessException("Subscriber detaching is not supported");

		public static bool TryAddEventHandler<T, H>(this EventAccess<T, H> accessor, in T instance, H handler)
			where H : MulticastDelegate
			=> accessor(in instance, handler, EventAction.AddHandler);

		public static bool TryRemoveEventHandler<T, H>(this EventAccess<T, H> accessor, in T instance, H handler)
			where H : MulticastDelegate
			=> accessor(in instance, handler, EventAction.RemoveHandler);

		public static void AddEventHandler<T, H>(this EventAccess<T, H> accessor, in T instance, H handler)
			where H : MulticastDelegate
		{
			if (!accessor.TryAddEventHandler(instance, handler))
				throw CannotAddHandler();
		}

		public static void RemoveEventHandler<T, H>(this EventAccess<T, H> accessor, in T instance, H handler)
			where H : MulticastDelegate
		{
			if (!accessor.TryRemoveEventHandler(instance, handler))
				throw CannotRemoveHandler();
		}

		public static bool TryAddEventHandler<H>(this EventAccess<H> accessor, H handler)
			where H : MulticastDelegate
			=> accessor(handler, EventAction.AddHandler);

		public static bool TryRemoveEventHandler<H>(this EventAccess<H> accessor, H handler)
			where H : MulticastDelegate
			=> accessor(handler, EventAction.RemoveHandler);

		public static void AddEventHandler<H>(this EventAccess<H> accessor, H handler)
			where H : MulticastDelegate
		{
			if (!accessor.TryAddEventHandler(handler))
				throw CannotAddHandler();
		}

		public static void RemoveEventHandler<H>(this EventAccess<H> accessor, H handler)
			where H : MulticastDelegate
		{
			if (!accessor.TryRemoveEventHandler(handler))
				throw CannotRemoveHandler();
		}
	}
}
