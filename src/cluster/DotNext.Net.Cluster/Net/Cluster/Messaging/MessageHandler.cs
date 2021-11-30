using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Messaging;

using Runtime.CompilerServices;

/// <summary>
/// Represents base class for declaring typed message handlers.
/// </summary>
/// <remarks>
/// The handler can be constructed in two ways: using <see cref="MessageHandler.Builder"/>
/// and through inheritance. If you choose the inheritance then message handlers must be declared
/// as public instance methods with one of the following signatures:
/// <code>
/// // duplex message handlers
/// Task&lt;Result&gt; HandleAsync(InputMessage input, CancellationToken token);
/// Task&lt;Result&gt; HandleAsync(ISubscriber sender, InputMessage input, CancellationToken token);
/// Task&lt;Result&gt; HandleAsync(InputMessage input, object? context, CancellationToken token);
/// Task&lt;Result&gt; HandleAsync(ISubscriber sender, InputMessage input, object? context, CancellationToken token);
/// // signal message handlers
/// Task HandleAsync(InputMessage input, CancellationToken token);
/// Task HandleAsync(ISubscriber sender, InputMessage input, CancellationToken token);
/// Task HandleAsync(InputMessage input, object? context, CancellationToken token);
/// Task HandleAsync(ISubscriber sender, InputMessage input, object? context, CancellationToken token);
/// </code>
/// Otherwise, command handlers can be registered through the builder.
/// </remarks>
[RequiresPreviewFeatures]
public partial class MessageHandler : IInputChannel
{
    private readonly IReadOnlyDictionary<string, RpcHandler> rpcHandlers;
    private readonly IReadOnlyDictionary<string, SignalHandler> signalHandlers;

    /// <summary>
    /// Initializes a new typed message handler and discover all methods suitable for handling messages.
    /// </summary>
    [RuntimeFeatures(RuntimeGenericInstantiation = true)]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(RpcHandler<,>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(SignalHandler<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Func<,,>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Func<,,,>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Func<,,,,>))]
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    protected MessageHandler()
    {
        // inspect message types
        var messages = new Dictionary<Type, MessageAttribute>();
        foreach (var attribute in GetType().GetCustomAttributes<MessageAttribute>())
        {
            var type = attribute.GetType();

            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(MessageAttribute<>))
            {
                type = type.GetGenericArguments()[0];
                messages.Add(type, attribute);
            }
        }

        // inspect handlers
        const BindingFlags publicInstanceMethod = BindingFlags.Public | BindingFlags.Instance;
        var methods = GetType().GetMethods(publicInstanceMethod);
        var rpcHandlers = new Dictionary<string, RpcHandler>(methods.Length, StringComparer.Ordinal);
        var signalHandlers = new Dictionary<string, SignalHandler>(methods.Length, StringComparer.Ordinal);

        foreach (var method in methods)
        {
            Type inputType, returnType = method.ReturnType;
            bool isSignalHandler;
            Delegate handler;

            if (returnType == typeof(Task))
            {
                isSignalHandler = true;
                returnType = typeof(void);
            }
            else if (returnType.IsConstructedGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // rpc handler
                returnType = returnType.GetGenericArguments()[0];
                isSignalHandler = false;
            }
            else
            {
                continue;
            }

            // construct message handler delegate
            var parameters = method.GetParameters();

            switch (parameters.Length)
            {
                default:
                    continue;
                case 2:
                    // TInput, CancellationToken
                    inputType = parameters[0].ParameterType;

                    if (parameters[1].ParameterType == typeof(CancellationToken))
                    {
                        handler = method.CreateDelegate(typeof(Func<,,>).MakeGenericType(inputType, typeof(CancellationToken), method.ReturnType), this);
                        break;
                    }

                    continue;
                case 3:
                    if (parameters[2].ParameterType == typeof(CancellationToken))
                    {
                        if (parameters[0].ParameterType == typeof(ISubscriber))
                        {
                            // ISubscriber, TInput, CancellationToken
                            inputType = parameters[1].ParameterType;
                            handler = method.CreateDelegate(typeof(Func<,,,>).MakeGenericType(typeof(ISubscriber), inputType, typeof(CancellationToken), method.ReturnType), this);
                            break;
                        }
                        else if (parameters[1].ParameterType == typeof(object))
                        {
                            // TInput, object, CancellationToken
                            inputType = parameters[0].ParameterType;
                            handler = method.CreateDelegate(typeof(Func<,,,>).MakeGenericType(inputType, typeof(object), typeof(CancellationToken), method.ReturnType), this);
                            break;
                        }
                    }

                    continue;
                case 4:
                    // ISubscriber, TInput, object, CancellationToken
                    inputType = parameters[1].ParameterType;

                    if (parameters[0].ParameterType == typeof(ISubscriber) && parameters[2].ParameterType == typeof(object) && parameters[3].ParameterType == typeof(CancellationToken))
                    {
                        handler = method.CreateDelegate(typeof(Func<,,,,>).MakeGenericType(typeof(ISubscriber), inputType, typeof(object), typeof(CancellationToken), method.ReturnType), this);
                        break;
                    }

                    continue;
            }

            // instantiate handler and register it
            var inputMessageInfo = messages[inputType];
            if (isSignalHandler)
            {
                object[] args = { handler };
                var d = Activator.CreateInstance(typeof(SignalHandler<>).MakeGenericType(inputType), args) as ISupplier<Delegate>;
                if (d?.Invoke() is SignalHandler signal)
                    signalHandlers.Add(inputMessageInfo.Name, signal);
            }
            else
            {
                var outputMessageInfo = messages[returnType];
                object[] args = { handler, outputMessageInfo.Name, outputMessageInfo.MimeType };
                var d = Activator.CreateInstance(typeof(RpcHandler<,>).MakeGenericType(inputType, returnType), args) as ISupplier<Delegate>;
                if (d?.Invoke() is RpcHandler rpc)
                    rpcHandlers.Add(inputMessageInfo.Name, rpc);
            }
        }

        rpcHandlers.TrimExcess();
        this.rpcHandlers = rpcHandlers;

        signalHandlers.TrimExcess();
        this.signalHandlers = signalHandlers;
    }

    private MessageHandler(IDictionary<string, RpcHandler> rpcHandlers, IDictionary<string, SignalHandler> signalHandlers)
    {
        this.rpcHandlers = ImmutableDictionary.ToImmutableDictionary(rpcHandlers);
        this.signalHandlers = ImmutableDictionary.ToImmutableDictionary(signalHandlers);
    }

    /// <inheritdoc/>
    bool IInputChannel.IsSupported(string messageName, bool oneWay)
        => oneWay ? signalHandlers.ContainsKey(messageName) : rpcHandlers.ContainsKey(messageName);

    /// <inheritdoc/>
    Task<IMessage> IInputChannel.ReceiveMessage(ISubscriber sender, IMessage message, object? context, CancellationToken token)
        => rpcHandlers.TryGetValue(message.Name, out var handler) ? handler(sender, message, context, token) : Task.FromException<IMessage>(new NotSupportedException());

    /// <inheritdoc/>
    Task IInputChannel.ReceiveSignal(ISubscriber sender, IMessage signal, object? context, CancellationToken token)
        => signalHandlers.TryGetValue(signal.Name, out var handler) ? handler(sender, signal, context, token) : Task.FromException(new NotSupportedException());
}