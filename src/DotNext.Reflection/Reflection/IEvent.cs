using System;
using System.Diagnostics.CodeAnalysis;
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
    /// <typeparam name="THandler">Type of event handler.</typeparam>
    public interface IEvent<in THandler> : IEvent
        where THandler : MulticastDelegate
    {
        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="handler">An event handler to add.</param>
        void AddEventHandler(THandler handler);

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="handler">An event handler to remove.</param>
        void RemoveEventHandler(THandler handler);
    }

    /// <summary>
    /// Represents instance event.
    /// </summary>
    /// <typeparam name="T">Type of event declaring type.</typeparam>
    /// <typeparam name="THandler">Type of event handler.</typeparam>
    public interface IEvent<T, in THandler> : IEvent
        where THandler : MulticastDelegate
    {
        /// <summary>
        /// Add event handler.
        /// </summary>
        /// <param name="instance">Object with declared event.</param>
        /// <param name="handler">An event handler to add.</param>
        void AddEventHandler([DisallowNull] in T instance, THandler handler);

        /// <summary>
        /// Remove event handler.
        /// </summary>
        /// <param name="instance">Object with declared event.</param>
        /// <param name="handler">An event handler to remove.</param>
        void RemoveEventHandler([DisallowNull] in T instance, THandler handler);
    }
}