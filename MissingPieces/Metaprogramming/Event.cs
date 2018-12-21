using System;
using System.Collections.Generic;
using System.Reflection;

namespace MissingPieces.Metaprogramming
{
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
}