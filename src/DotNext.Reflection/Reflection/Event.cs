using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected event.
    /// </summary>
    /// <typeparam name="THandler">A delegate representing event handler.</typeparam>
    public class EventBase<THandler> : EventInfo, IEvent, IEquatable<EventInfo?>
        where THandler : MulticastDelegate
    {
        private readonly EventInfo @event;

        private protected EventBase(EventInfo @event) => this.@event = @event;

        private static bool AddOrRemoveHandler(EventInfo @event, object? target, THandler handler, Action<object?, Delegate> modifier)
        {
            if (@event.AddMethod?.IsStatic ?? false)
            {
                if (target is null)
                {
                    modifier(null, handler);
                    return true;
                }
            }
            else if (@event.DeclaringType?.IsInstanceOfType(target) ?? false)
            {
                modifier(target, handler);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds an event handler to an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">Encapsulates a method or methods to be invoked when the event is raised by the target.</param>
        /// <returns><see langword="true"/>, if arguments are correct; otherwise, <see langword="false"/>.</returns>
        public virtual bool AddEventHandler(object? target, THandler handler)
            => AddOrRemoveHandler(@event, target, handler, @event.AddEventHandler);

        /// <summary>
        /// Removes an event handler from an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">The delegate to be disassociated from the events raised by target.</param>
        /// <returns><see langword="true"/>, if arguments are correct; otherwise, <see langword="false"/>.</returns>
        public virtual bool RemoveEventHandler(object? target, THandler handler)
            => AddOrRemoveHandler(@event, target, handler, @event.RemoveEventHandler);

        /// <inheritdoc/>
        EventInfo IMember<EventInfo>.RuntimeMember => @event;

        /// <summary>
        /// Gets the class that declares this constructor.
        /// </summary>
        public sealed override Type? DeclaringType => @event.DeclaringType;

        /// <summary>
        /// Always returns <see cref="MemberTypes.Event"/>.
        /// </summary>
        public sealed override MemberTypes MemberType => @event.MemberType;

        /// <summary>
        /// Gets name of the event.
        /// </summary>
        public sealed override string Name => @event.Name;

        /// <summary>
        /// Gets the class object that was used to obtain this instance.
        /// </summary>
        public sealed override Type? ReflectedType => @event.ReflectedType;

        /// <summary>
        /// Returns an array of all custom attributes applied to this event.
        /// </summary>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns>An array that contains all the custom attributes applied to this event.</returns>
        public sealed override object[] GetCustomAttributes(bool inherit) => @event.GetCustomAttributes(inherit);

        /// <summary>
        /// Returns an array of all custom attributes applied to this event.
        /// </summary>
        /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned.</param>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns>An array that contains all the custom attributes applied to this event.</returns>
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => @event.GetCustomAttributes(attributeType, inherit);

        /// <summary>
        /// Determines whether one or more attributes of the specified type or of its derived types is applied to this event.
        /// </summary>
        /// <param name="attributeType">The type of custom attribute to search for. The search includes derived types.</param>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if one or more instances of <paramref name="attributeType"/> or any of its derived types is applied to this event; otherwise, <see langword="false"/>.</returns>
        public sealed override bool IsDefined(Type attributeType, bool inherit) => @event.IsDefined(attributeType, inherit);

        /// <summary>
        /// Gets a value that identifies a metadata element.
        /// </summary>
        public sealed override int MetadataToken => @event.MetadataToken;

        /// <summary>
        /// Gets the module in which the type that declares the event represented by the current instance is defined.
        /// </summary>
        public sealed override Module Module => @event.Module;

        /// <summary>
        /// Returns a list of custom attributes that have been applied to the target event.
        /// </summary>
        /// <returns>The data about the attributes that have been applied to the target event.</returns>
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => @event.GetCustomAttributesData();

        /// <summary>
        /// Gets a collection that contains this member's custom attributes.
        /// </summary>
        public sealed override IEnumerable<CustomAttributeData> CustomAttributes => @event.CustomAttributes;

        /// <summary>
        /// Gets the attributes associated with this event.
        /// </summary>
        public sealed override EventAttributes Attributes => @event.Attributes;

        /// <summary>
        /// Gets a value indicating whether the event is multicast.
        /// </summary>
        public sealed override bool IsMulticast => @event.IsMulticast;

        /// <summary>
        /// Gets the the underlying event-handler delegate associated with this event.
        /// </summary>
        public sealed override Type? EventHandlerType => @event.EventHandlerType;

        /// <summary>
        /// Gets event subscription method.
        /// </summary>
        public sealed override MethodInfo? AddMethod => @event.AddMethod;

        /// <summary>
        /// Gets the method that is called when the event is raised, including non-public methods.
        /// </summary>
        public sealed override MethodInfo? RaiseMethod => @event.RaiseMethod;

        /// <summary>
        /// Gets event unsubscription method.
        /// </summary>
        public sealed override MethodInfo? RemoveMethod => @event.RemoveMethod;

        /// <summary>
        /// Gets event subscription method.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> if non-public methods can be returned; otherwise, <see langword="false"/>.</param>
        /// <returns>Event subscription method.</returns>
        /// <exception cref="MethodAccessException">
        /// <paramref name="nonPublic"/> is <see langword="true"/>, the method used to add an event handler delegate is non-public,
        /// and the caller does not have permission to reflect on non-public methods.
        /// </exception>
        public sealed override MethodInfo? GetAddMethod(bool nonPublic) => @event.GetAddMethod(nonPublic);

        /// <summary>
        /// Gets event unsubscription method.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> if non-public methods can be returned; otherwise, <see langword="false"/>.</param>
        /// <returns>Event unsubscription method.</returns>
        /// <exception cref="MethodAccessException">
        /// <paramref name="nonPublic"/> is <see langword="true"/>, the method used to remove an event handler delegate is non-public,
        /// and the caller does not have permission to reflect on non-public methods.
        /// </exception>
        public sealed override MethodInfo? GetRemoveMethod(bool nonPublic) => @event.GetRemoveMethod(nonPublic);

        /// <summary>
        /// Gets the method that is called when the event is raised.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> if non-public methods can be returned; otherwise, <see langword="false"/>.</param>
        /// <returns>Raise method.</returns>
        /// <exception cref="MethodAccessException">
        /// <paramref name="nonPublic"/> is <see langword="true"/>, the method is non-public, and the caller does not have permission to reflect on non-public methods.
        /// </exception>
        public sealed override MethodInfo? GetRaiseMethod(bool nonPublic) => @event.GetRaiseMethod(nonPublic);

        /// <summary>
        /// Returns the methods that have been associated with the event in metadata using the <c>.other</c> directive, specifying whether to include non-public methods.
        /// </summary>
        /// <param name="nonPublic"><see langword="true"/> if non-public methods can be returned; otherwise, <see langword="false"/>.</param>
        /// <returns>An array of event methods.</returns>
        public sealed override MethodInfo[] GetOtherMethods(bool nonPublic) => @event.GetOtherMethods();

        /// <summary>
        /// Determines whether this event is equal to the given event.
        /// </summary>
        /// <param name="other">Other event to compare.</param>
        /// <returns><see langword="true"/> if this object reflects the same event as the specified object; otherwise, <see langword="false"/>.</returns>
        public bool Equals(EventInfo? other) => other is Event<THandler> @event ? this.@event == @event.@event : this.@event == other;

        /// <summary>
        /// Determines whether this event is equal to the given event.
        /// </summary>
        /// <param name="other">Other event to compare.</param>
        /// <returns><see langword="true"/> if this object reflects the same event as the specified object; otherwise, <see langword="false"/>.</returns>
        public sealed override bool Equals(object? other) => other switch
        {
            EventBase<THandler> @event => @event.@event == this.@event,
            EventInfo @event => this.@event == @event,
            _ => false,
        };

        /// <summary>
        /// Computes hash code uniquely identifies the reflected event.
        /// </summary>
        /// <returns>The hash code of the event.</returns>
        public sealed override int GetHashCode() => @event.GetHashCode();

        /// <summary>
        /// Returns textual representation of this event.
        /// </summary>
        /// <returns>The textual representation of this event.</returns>
        public sealed override string? ToString() => @event.ToString();
    }

    /// <summary>
    /// Provides typed access to static event declared in type <typeparamref name="THandler"/>.
    /// </summary>
    /// <typeparam name="THandler">Type of event handler.</typeparam>
    public sealed class Event<THandler> : EventBase<THandler>, IEvent<THandler>
        where THandler : MulticastDelegate
    {
        private sealed class Cache<T> : MemberCache<EventInfo, Event<THandler>>
        {
            private protected override Event<THandler>? Create(string eventName, bool nonPublic) => Reflect(typeof(T), eventName, nonPublic);
        }

        private const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
        private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly Action<THandler>? addMethod, removeMethod;

        private Event(EventInfo @event)
            : base(@event)
        {
            addMethod = @event.AddMethod?.CreateDelegate<Action<THandler>>();
            removeMethod = @event.RemoveMethod?.CreateDelegate<Action<THandler>>();
        }

        /// <summary>
        /// Adds static event handler.
        /// </summary>
        /// <param name="target">Should be <see langword="null"/>.</param>
        /// <param name="handler">Encapsulates a method or methods to be invoked when the event is raised by the target.</param>
        /// <returns><see langword="true"/>, if <paramref name="target"/> is <see langword="null"/>, <see langword="false"/>.</returns>
        public override bool AddEventHandler(object? target, THandler handler)
        {
            if (target is null && addMethod is not null)
            {
                addMethod(handler);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes static event handler.
        /// </summary>
        /// <param name="target">Should be <see langword="null"/>.</param>
        /// <param name="handler">The delegate to be disassociated from the events raised by target.</param>
        /// <returns><see langword="true"/>, if <paramref name="target"/> is <see langword="null"/>, <see langword="false"/>.</returns>
        public override bool RemoveEventHandler(object? target, THandler handler)
        {
            if (target is null && removeMethod is not null)
            {
                removeMethod(handler);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="handler">An event handler to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEventHandler(THandler handler) => addMethod?.Invoke(handler);

        /// <summary>
        /// Adds static event handler.
        /// </summary>
        /// <param name="target">Should be <see langword="null"/>.</param>
        /// <param name="handler">Encapsulates a method or methods to be invoked when the event is raised by the target.</param>
        public override void AddEventHandler(object? target, Delegate? handler)
        {
            if (handler is THandler typedHandler)
                AddEventHandler(typedHandler);
            else
                base.AddEventHandler(target, handler);
        }

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="handler">An event handler to remove.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEventHandler(THandler handler) => removeMethod?.Invoke(handler);

        /// <summary>
        /// Removes static event handler.
        /// </summary>
        /// <param name="target">Should be <see langword="null"/>.</param>
        /// <param name="handler">The delegate to be disassociated from the events raised by target.</param>
        public override void RemoveEventHandler(object? target, Delegate? handler)
        {
            if (handler is THandler typedHandler)
                RemoveEventHandler(typedHandler);
            else
                base.RemoveEventHandler(target, handler);
        }

        /// <summary>
        /// Returns a delegate which can be used to attach new handlers to the event.
        /// </summary>
        /// <param name="event">Reflected event.</param>
        /// <returns>The delegate which can be used to attach new handlers to the event.</returns>
        [return: NotNullIfNotNull("event")]
        public static Action<THandler>? operator +(Event<THandler>? @event) => @event?.addMethod;

        /// <summary>
        /// Returns a delegate which can be used to detach from the event.
        /// </summary>
        /// <param name="event">Reflected event.</param>
        /// <returns>The delegate which can be used to detach from the event.</returns>
        [return: NotNullIfNotNull("event")]
        public static Action<THandler>? operator -(Event<THandler>? @event) => @event?.removeMethod;

        private static Event<THandler>? Reflect(Type declaringType, string eventName, bool nonPublic)
        {
            EventInfo? @event = declaringType.GetEvent(eventName, nonPublic ? NonPublicFlags : PublicFlags);
            return @event is null ? null : new Event<THandler>(@event);
        }

        internal static Event<THandler>? GetOrCreate<T>(string eventName, bool nonPublic)
            => Cache<T>.Of<Cache<T>>(typeof(T)).GetOrCreate(eventName, nonPublic);
    }

    /// <summary>
    /// Provides typed access to instance event declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="THandler">Type of event handler.</typeparam>
    public sealed class Event<T, THandler> : EventBase<THandler>, IEvent<T, THandler>
        where THandler : MulticastDelegate
    {
        private sealed class Cache : MemberCache<EventInfo, Event<T, THandler>>
        {
            private protected override Event<T, THandler>? Create(string eventName, bool nonPublic) => Reflect(eventName, nonPublic);
        }

        private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Represents event accessor.
        /// </summary>
        /// <param name="instance">The event target.</param>
        /// <param name="handler">The event handler.</param>
        public delegate void Accessor([DisallowNull]in T instance, THandler handler);

        private readonly Accessor? addMethod, removeMethod;

        private Event(EventInfo @event)
            : base(@event)
        {
            if (@event.DeclaringType is null)
                throw new ArgumentException(ExceptionMessages.ModuleMemberDetected(@event), nameof(@event));
            var instanceParam = Expression.Parameter(@event.DeclaringType.MakeByRefType());
            if (@event.EventHandlerType is null)
            {
                addMethod = removeMethod = null;
            }
            else
            {
                var handlerParam = Expression.Parameter(@event.EventHandlerType);
                addMethod = @event.AddMethod is null ? null : CompileAccessor(@event.AddMethod, instanceParam, handlerParam);
                removeMethod = @event.RemoveMethod is null ? null : CompileAccessor(@event.RemoveMethod, instanceParam, handlerParam);
            }
        }

        private static Accessor CompileAccessor(MethodInfo accessor, ParameterExpression instanceParam, ParameterExpression handlerParam)
        {
            if (accessor.DeclaringType is null)
                throw new ArgumentException(ExceptionMessages.ModuleMemberDetected(accessor), nameof(accessor));
            if (accessor.DeclaringType.IsValueType)
                return accessor.CreateDelegate<Accessor>();
            return Expression.Lambda<Accessor>(
                Expression.Call(instanceParam, accessor, handlerParam), instanceParam, handlerParam).Compile();
        }

        /// <summary>
        /// Adds an event handler to an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">Encapsulates a method or methods to be invoked when the event is raised by the target.</param>
        /// <returns><see langword="true"/>, if arguments are correct; otherwise, <see langword="false"/>.</returns>
        public override bool AddEventHandler(object? target, THandler handler)
        {
            if (target is T instance && addMethod is not null)
            {
                addMethod(instance, handler);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes an event handler from an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">The delegate to be disassociated from the events raised by target.</param>
        /// <returns><see langword="true"/>, if arguments are correct; otherwise, <see langword="false"/>.</returns>
        public override bool RemoveEventHandler(object? target, THandler handler)
        {
            if (target is T instance && removeMethod is not null)
            {
                removeMethod(instance, handler);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds an event handler to an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">Encapsulates a method or methods to be invoked when the event is raised by the target.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEventHandler([DisallowNull]in T target, THandler handler)
            => addMethod?.Invoke(in target, handler);

        /// <summary>
        /// Adds an event handler to an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">Encapsulates a method or methods to be invoked when the event is raised by the target.</param>
        public override void AddEventHandler(object? target, Delegate? handler)
        {
            if (target is T typedTarget && handler is THandler typedHandler)
                AddEventHandler(typedTarget, typedHandler);
            else
                base.AddEventHandler(target, handler);
        }

        /// <summary>
        /// Removes an event handler from an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">The delegate to be disassociated from the events raised by target.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEventHandler([DisallowNull]in T target, THandler handler)
            => removeMethod?.Invoke(in target, handler);

        /// <summary>
        /// Removes an event handler from an event source.
        /// </summary>
        /// <param name="target">The event source.</param>
        /// <param name="handler">The delegate to be disassociated from the events raised by target.</param>
        public override void RemoveEventHandler(object? target, Delegate? handler)
        {
            if (target is T typedTarget && handler is THandler typedHandler)
                RemoveEventHandler(typedTarget, typedHandler);
            else
                base.RemoveEventHandler(target, handler);
        }

        /// <summary>
        /// Returns a delegate which can be used to attach new handlers to the event.
        /// </summary>
        /// <param name="event">Reflected event.</param>
        /// <returns>The delegate which can be used to attach new handlers to the event.</returns>
        [return: NotNullIfNotNull("event")]
        public static Accessor? operator +(Event<T, THandler>? @event) => @event?.addMethod;

        /// <summary>
        /// Returns a delegate which can be used to detach from the event.
        /// </summary>
        /// <param name="event">Reflected event.</param>
        /// <returns>The delegate which can be used to detach from the event.</returns>
        [return: NotNullIfNotNull("event")]
        public static Accessor? operator -(Event<T, THandler>? @event) => @event?.removeMethod;

        private static Event<T, THandler>? Reflect(string eventName, bool nonPublic)
        {
            EventInfo? @event = typeof(T).GetEvent(eventName, nonPublic ? NonPublicFlags : PublicFlags);
            return @event is null ? null : new Event<T, THandler>(@event);
        }

        internal static Event<T, THandler>? GetOrCreate(string eventName, bool nonPublic)
            => Cache.Of<Cache>(typeof(T)).GetOrCreate(eventName, nonPublic);
    }
}