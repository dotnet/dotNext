using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected event.
    /// </summary>
    public interface IEvent : IMember<EventInfo>
    {
    }

    /// <summary>
    /// Represents static event.
    /// </summary>
    /// <typeparam name="H">Type of event handler.</typeparam>
    public interface IEvent<in H> : IEvent
        where H : MulticastDelegate
    {
        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="handler">An event handler to add.</param>
        void AddEventHandler(H handler);

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="handler">An event handler to remove.</param>
        void RemoveEventHandler(H handler);
    }

    /// <summary>
    /// Represents instance event.
    /// </summary>
    /// <typeparam name="T">Type of event declaring type.</typeparam>
    /// <typeparam name="H">Type of event handler.</typeparam>
    public interface IEvent<T, in H> : IEvent
        where H : MulticastDelegate
    {
        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="instance">Object with declared event.</param>
        /// <param name="handler">An event handler to add.</param>
        void AddEventHandler(in T instance, H handler);

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="instance">Object with declared event.</param>
        /// <param name="handler">An event handler to remove.</param>
        void RemoveEventHandler(in T instance, H handler);
    }
}