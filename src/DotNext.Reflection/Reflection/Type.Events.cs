using System;

namespace DotNext.Reflection
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
            /// <summary>
            /// Gets instance event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public event.</param>
            /// <returns>Instance event; or <see langword="null"/>, if event doesn't exist.</returns>
            public static Event<T, H> Get(string eventName, bool nonPublic = false)
                => Event<T, H>.GetOrCreate(eventName, nonPublic);

            /// <summary>
            /// Gets instance event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public event.</param>
            /// <returns>Instance event.</returns>
            /// <exception cref="MissingEventException">Event doesn't exist.</exception>
            public static Event<T, H> Require(string eventName, bool nonPublic = false)
                => Get(eventName, nonPublic) ?? throw MissingEventException.Create<T, H>(eventName);

            /// <summary>
            /// Gets static event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public event.</param>
            /// <returns>Static event; or <see langword="null"/>, if event doesn't exist.</returns>
            public static Reflection.Event<H> GetStatic(string eventName, bool nonPublic = false)
                => Reflection.Event<H>.GetOrCreate<T>(eventName, nonPublic);

            /// <summary>
            /// Gets static event.
            /// </summary>
            /// <param name="eventName">Name of event.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public event.</param>
            /// <returns>Static event.</returns>
            /// <exception cref="MissingEventException">Event doesn't exist.</exception>
            public static Reflection.Event<H> RequireStatic(string eventName, bool nonPublic = false)
                => GetStatic(eventName, nonPublic) ?? throw MissingEventException.Create<T, H>(eventName);
        }
    }
}