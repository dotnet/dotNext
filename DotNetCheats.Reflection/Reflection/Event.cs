using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;

namespace DotNetCheats.Reflection
{
    /// <summary>
    /// Represents reflected event.
    /// </summary>
    /// <typeparam name="D">A delegate representing event handler.</typeparam>
    public class EventBase<D> : EventInfo, IEvent, IEquatable<EventBase<D>>, IEquatable<EventInfo>
        where D : MulticastDelegate
    {
        private readonly EventInfo @event;

        private protected EventBase(EventInfo @event)
        {
            this.@event = @event;
        }

        private static bool AddOrRemoveHandler(EventInfo @event, object target, D handler, Action<object, Delegate> modifier)
        {
            if(@event.AddMethod.IsStatic)
            {
                if(target is null)
                {
                    modifier(target, handler);
                    return true;
                }
            }
            else if(@event.DeclaringType.IsInstanceOfType(target))
            {
                modifier(target, handler);
                return true;
            }
            return false;
        }

        public virtual bool AddEventHandler(object target, D handler)
            => AddOrRemoveHandler(@event, target, handler, @event.AddEventHandler);
        public virtual bool RemoveEventHandler(object target, D handler)
            => AddOrRemoveHandler(@event, target, handler, @event.RemoveEventHandler);

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

        public static bool operator ==(EventBase<D> first, EventBase<D> second) => Equals(first, second);

        public static bool operator !=(EventBase<D> first, EventBase<D> second) => !Equals(first, second);

        public bool Equals(EventInfo other) => @event == other;

        public bool Equals(EventBase<D> other)
            => other != null &&
                GetType() == other.GetType() &&
                Equals(other.@event);

        public sealed override bool Equals(object other)
        {
            switch (other)
            {
                case EventBase<D> @event:
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
    /// <typeparam name="D">Type of event handler.</typeparam>
    public sealed class Event<D> : EventBase<D>, IEvent<D>
        where D : MulticastDelegate
    {
        private const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly Action<D> addMethod;
        private readonly Action<D> removeMethod;

        private Event(EventInfo @event)
            : base(@event)
        {
            var addMethod = @event.AddMethod;
            var removeMethod = @event.RemoveMethod;
            this.addMethod = addMethod is null ? null : addMethod.CreateDelegate<Action<D>>();
            this.removeMethod = removeMethod is null ? null : removeMethod.CreateDelegate<Action<D>>();
        }

        public override bool AddEventHandler(object target, D handler)
        {
            if(target is null){
                addMethod(handler);
                return true;
            }
            else
                return false;
        }
        public override bool RemoveEventHandler(object target, D handler)
        {
            if(target is null)
            {
                removeMethod(handler);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="handler">An event handler to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEventHandler(D handler) => addMethod(handler);

        public override void AddEventHandler(object target, Delegate handler)
        {
            if (handler is D typedHandler)
                AddEventHandler(typedHandler);
            else
                base.AddEventHandler(target, handler);
        }

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="handler">An event handler to remove.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveEventHandler(D handler) => removeMethod(handler);

        public override void RemoveEventHandler(object target, Delegate handler)
        {
            if (handler is D typedHandler)
                RemoveEventHandler(typedHandler);
            else
                base.RemoveEventHandler(target, handler);
        }

        public static Action<D> operator +(Event<D> @event) => @event.addMethod;
        public static Action<D> operator -(Event<D> @event) => @event.removeMethod;

        internal static Event<D> Reflect<T>(string eventName, bool nonPublic)
        {
            var @event = typeof(T).GetEvent(eventName, nonPublic ? NonPublicFlags : PublicFlags);
            return @event is null ? null : new Event<D>(@event);
        }

        internal static Event<D> Reflect(EventInfo @event)
        {
            if(@event is Event<D> other)
                return other;
            else if(@event.EventHandlerType == typeof(D))
                return new Event<D>(@event);
            else
                return null;
        }
    }

    /// <summary>
    /// Provides typed access to instance event declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="D">Type of event handler.</typeparam>
    public sealed class Event<T, D> : EventBase<D>, IEvent<T, D>
        where D : MulticastDelegate
    {
        private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public delegate void Accessor(in T instance, D handler);

        private readonly Accessor addMethod;
        private readonly Accessor removeMethod;

        private Event(EventInfo @event)
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

        public override bool AddEventHandler(object target, D handler)
        {
            if(target is T instance){
                addMethod(instance, handler);
                return true;
            }
            else
                return false;
        }
        public override bool RemoveEventHandler(object target, D handler)
        {
            if(target is T instance)
            {
                removeMethod(instance, handler);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="instance">Object with declared event.</param>
        /// <param name="handler">An event handler to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddEventHandler(in T instance, D handler)
            => addMethod(in instance, handler);

        public override void AddEventHandler(object target, Delegate handler)
        {
            if (target is T typedTarget && handler is D typedHandler)
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
        public void RemoveEventHandler(in T instance, D handler)
            => removeMethod(in instance, handler);

        public override void RemoveEventHandler(object target, Delegate handler)
        {
            if (target is T typedTarget && handler is D typedHandler)
                RemoveEventHandler(typedTarget, typedHandler);
            else
                base.RemoveEventHandler(target, handler);
        }

        public static Accessor operator+(Event<T, D> @event) => @event.addMethod;

        public static Accessor operator-(Event<T, D> @event) => @event.removeMethod;

        internal static Event<T, D> Reflect(string eventName, bool nonPublic)
        {
            var @event = typeof(T).GetEvent(eventName, nonPublic ? NonPublicFlags : PublicFlags);
            return @event is null ? null : new Event<T, D>(@event);
        }
        
        internal static Event<T, D> Reflect(EventInfo @event)
        {
            if(@event is Event<T, D> other)
                return other;
            else if(@event.EventHandlerType == typeof(D) && @event.DeclaringType == typeof(T))
                return new Event<T, D>(@event);
            else
                return null;
        }
    }
}