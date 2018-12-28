using System;
using System.Reflection;

namespace DotNetCheats.Reflection
{
    public static partial class Type<T>
    {
         /// <summary>
        /// Provides typed access to instance event declared in type <typeparamref name="T"/>.
        /// </summary>
		/// <typeparam name="H">Type of event handler.</typeparam>
        public static class Event<H>
            where H : MulticastDelegate
        {
            private sealed class InstanceEvents : MemberCache<EventInfo, Reflection.Event<T, H>>
            {
                internal static readonly InstanceEvents Public = new InstanceEvents(false);
                internal static readonly InstanceEvents NonPublic = new InstanceEvents(true);

                private readonly bool nonPublic;
                private InstanceEvents(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Reflection.Event<T, H> Create(string eventName) 
                    => Reflection.Event<T, H>.Reflect(eventName, nonPublic);
            }

            private sealed class StaticEvents : MemberCache<EventInfo, Reflection.Event<H>>
            {
                internal static readonly StaticEvents Public = new StaticEvents(false);
                internal static readonly StaticEvents NonPublic = new StaticEvents(true);
                private readonly bool nonPublic;
                private StaticEvents(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Reflection.Event<H> Create(string eventName) 
                    => Reflection.Event<H>.Reflect<T>(eventName, nonPublic);
            }

            /// <summary>
            /// Gets instane event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic">True to reflect non-public event.</param>
            /// <returns>Instance event; or null, if event doesn't exist.</returns>
            public static Reflection.Event<T, H> Get(string eventName, bool nonPublic = false)
                => (nonPublic ? InstanceEvents.NonPublic : InstanceEvents.Public).GetOrCreate(eventName);

            /// <summary>
            /// Gets instance event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic">True to reflect non-public event.</param>
            /// <returns>Instance event.</returns>
            /// <exception cref="MissingEventException">Event doesn't exist.</exception>
            public static Reflection.Event<T, H> Require(string eventName, bool nonPublic = false)
                => Get(eventName, nonPublic) ?? throw MissingEventException.Create<T, H>(eventName);

            /// <summary>
            /// Gets static event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic">True to reflect non-public event.</param>
            /// <returns>Static event; or null, if event doesn't exist.</returns>
            public static Reflection.Event<H> GetStatic(string eventName, bool nonPublic = false)
                => (nonPublic ? StaticEvents.NonPublic : StaticEvents.Public).GetOrCreate(eventName);

            /// <summary>
            /// Gets static event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic">True to reflect non-public event.</param>
            /// <returns>Static event.</returns>
            /// <exception cref="MissingEventException">Event doesn't exist.</exception>
            public static Reflection.Event<H> RequireStatic(string eventName, bool nonPublic = false)
                => GetStatic(eventName, nonPublic) ?? throw MissingEventException.Create<T, H>(eventName);
        }
    }
}