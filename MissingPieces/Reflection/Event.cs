using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;

namespace MissingPieces.Reflection
{
    /// <summary>
    /// Represents reflected event.
    /// </summary>
    /// <typeparam name="D">A delegate representing event handler.</typeparam>
    public abstract class Event<D> : EventInfo, IEvent, IEquatable<Event<D>>, IEquatable<EventInfo>
        where D : MulticastDelegate
    {
        private readonly EventInfo @event;

        private protected Event(EventInfo @event)
        {
            this.@event = @event;
        }

        EventInfo IMember<EventInfo>.RuntimeMember => @event;

        public sealed override Type DeclaringType => @event.DeclaringType;

        public sealed override MemberTypes MemberType => @event.MemberType;

        public sealed override string Name => @event.Name;

        public sealed override Type ReflectedType => @event.ReflectedType;

        public sealed override object[] GetCustomAttributes(bool inherit) => @event.GetCustomAttributes(inherit);
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => @event.GetCustomAttributes(attributeType, inherit);

        public sealed override bool IsDefined(Type attributeType, bool inherit) => @event.IsDefined(attributeType, inherit);

        public sealed override int MetadataToken => @event.MetadataToken;

        public sealed override Module Module => @event.Module;

        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => @event.GetCustomAttributesData();

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes => @event.CustomAttributes;

        public sealed override EventAttributes Attributes => @event.Attributes;

        public sealed override bool IsMulticast => @event.IsMulticast;

        public sealed override Type EventHandlerType => @event.EventHandlerType;

        public sealed override MethodInfo AddMethod => @event.AddMethod;
        public sealed override MethodInfo RaiseMethod => @event.RaiseMethod;
        public sealed override MethodInfo RemoveMethod => @event.RemoveMethod;

        public sealed override MethodInfo GetAddMethod(bool nonPublic) => @event.GetAddMethod(nonPublic);

        public sealed override MethodInfo GetRemoveMethod(bool nonPublic) => @event.GetRemoveMethod(nonPublic);

        public sealed override MethodInfo GetRaiseMethod(bool nonPublic) => @event.GetRaiseMethod(nonPublic);

        public sealed override MethodInfo[] GetOtherMethods(bool nonPublic) => @event.GetOtherMethods();

        public static bool operator ==(Event<D> first, Event<D> second) => Equals(first, second);

        public static bool operator !=(Event<D> first, Event<D> second) => !Equals(first, second);

        public bool Equals(EventInfo other) => @event == other;

        public bool Equals(Event<D> other)
            => other != null &&
                GetType() == other.GetType() &&
                Equals(other.@event);

        public sealed override bool Equals(object other)
        {
            switch (other)
            {
                case Event<D> @event:
                    return Equals(@event);
                case EventInfo @event:
                    return Equals(@event);
                default:
                    return false;
            }
        }

        public sealed override int GetHashCode() => @event.GetHashCode();

        public sealed override string ToString() => @event.ToString();
    }

    /// <summary>
    /// Provides typed access to static event declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="H">Type of event handler.</typeparam>
    public sealed class StaticEvent<H> : Event<H>, IEvent<H>
        where H : MulticastDelegate
    {
        private const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly Action<H> addMethod;
        private readonly Action<H> removeMethod;

        private StaticEvent(EventInfo @event)
            : base(@event)
        {
            var addMethod = @event.AddMethod;
            var removeMethod = @event.RemoveMethod;
            this.addMethod = addMethod is null ? null : addMethod.CreateDelegate<Action<H>>();
            this.removeMethod = removeMethod is null ? null : removeMethod.CreateDelegate<Action<H>>();
        }

        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="handler">An event handler to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEventHandler(H handler) => addMethod(handler);

        public override void AddEventHandler(object target, Delegate handler)
        {
            if (handler is H typedHandler)
                AddEventHandler(typedHandler);
            else
                base.AddEventHandler(target, handler);
        }

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="handler">An event handler to remove.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEventHandler(H handler) => removeMethod(handler);

        public override void RemoveEventHandler(object target, Delegate handler)
        {
            if (handler is H typedHandler)
                RemoveEventHandler(typedHandler);
            else
                base.RemoveEventHandler(target, handler);
        }

        public static Action<H> operator +(StaticEvent<H> @event) => @event.addMethod;
        public static Action<H> operator -(StaticEvent<H> @event) => @event.removeMethod;

        internal static StaticEvent<H> Reflect<T>(string eventName, bool nonPublic)
        {
            var @event = typeof(T).GetEvent(eventName, nonPublic ? NonPublicFlags : PublicFlags);
            return @event is null ? null : new StaticEvent<H>(@event);
        }
    }

    /// <summary>
    /// Provides typed access to instance event declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="H">Type of event handler.</typeparam>
    public sealed class InstanceEvent<T, H> : Event<H>, IEvent<T, H>
        where H : MulticastDelegate
    {
        private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public delegate void Accessor(in T instance, H handler);

        private readonly Accessor addMethod;
        private readonly Accessor removeMethod;

        private InstanceEvent(EventInfo @event)
            : base(@event)
        {
            var instanceParam = Expression.Parameter(@event.DeclaringType.MakeByRefType());
            var handlerParam = Expression.Parameter(@event.EventHandlerType);

            this.addMethod = CompileAccessor(@event.AddMethod, instanceParam, handlerParam);
            this.removeMethod = CompileAccessor(@event.RemoveMethod, instanceParam, handlerParam);
        }

        private static Accessor CompileAccessor(MethodInfo accessor, ParameterExpression instanceParam, ParameterExpression handlerParam)
        {
             if(accessor is null)
                return null;
            else if(accessor.DeclaringType.IsValueType)
                return accessor.CreateDelegate<Accessor>();
            else
                return Expression.Lambda<Accessor>(Expression.Call(instanceParam, accessor, handlerParam), instanceParam, handlerParam).Compile();
        }

        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="instance">Object with declared event.</param>
        /// <param name="handler">An event handler to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEventHandler(in T instance, H handler)
            => addMethod(in instance, handler);

        public override void AddEventHandler(object target, Delegate handler)
        {
            if (target is T typedTarget && handler is H typedHandler)
                AddEventHandler(typedTarget, typedHandler);
            else
                base.AddEventHandler(target, handler);
        }

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="instance">Object with declared event.</param>
        /// <param name="handler">An event handler to remove.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEventHandler(in T instance, H handler)
            => removeMethod(in instance, handler);

        public override void RemoveEventHandler(object target, Delegate handler)
        {
            if (target is T typedTarget && handler is H typedHandler)
                RemoveEventHandler(typedTarget, typedHandler);
            else
                base.RemoveEventHandler(target, handler);
        }

        public static Accessor operator+(InstanceEvent<T, H> @event) => @event.addMethod;

        public static Accessor operator-(InstanceEvent<T, H> @event) => @event.removeMethod;

        internal static InstanceEvent<T, H> Reflect(string eventName, bool nonPublic)
        {
            var @event = typeof(T).GetEvent(eventName, nonPublic ? NonPublicFlags : PublicFlags);
            return @event is null ? null : new InstanceEvent<T, H>(@event);
        }
    }
}