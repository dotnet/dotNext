using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using Missing = System.Reflection.Missing;
using static System.Threading.Timeout;
using ExceptionDispatchInfo = System.Runtime.ExceptionServices.ExceptionDispatchInfo;

namespace DotNext.Workflow;

using Metadata;
using Threading;

/// <summary>
/// Represents workflow execution engine.
/// </summary>
public abstract class WorkflowEngine : IActivityStateValidator, IActivityStartedCallback, IAsyncDisposable
{
    private readonly Dictionary<string, ActivityMetaModel> models;
    private readonly Dictionary<ActivityInstance, ActivityContext> instances;
    private readonly AsyncExclusiveLock stateLock;
    private readonly CancellationTokenSource lifecycleSource;
    private readonly CancellationToken lifecycleToken; // cached to avoid ObjectDisposedException
    private AtomicBoolean suspending;

    /// <summary>
    /// Initializes a new workflow engine.
    /// </summary>
    protected WorkflowEngine()
    {
        models = new(StringComparer.Ordinal);
        stateLock = new();
        lifecycleSource = new();
        lifecycleToken = lifecycleSource.Token;
        instances = new();
    }

    /// <summary>
    /// Registers an activity.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TActivity"></typeparam>
    /// <param name="activityFactory"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public WorkflowEngine RegisterActivity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TInput, TActivity>(Func<TActivity> activityFactory, ActivityOptions? options = null)
        where TInput : class
        where TActivity : Activity<TInput>
    {
        ArgumentNullException.ThrowIfNull(activityFactory);

        // create raw instance of activity
        var activity = RuntimeHelpers.GetUninitializedObject(typeof(TActivity)) as TActivity;
        Debug.Assert(activity is not null);
        GC.SuppressFinalize(activity);

        // catch special exception
        try
        {
            // this method will raise ActivityStateMachineDiscoveryException<T> exception that acts as a factory for
            // activity metamodel
            activity.ExecuteAsync(null!);
        }
        catch (ActivityStateMachineDiscoveryException e)
        {
            var model = e.CreateMetaModel<TInput, TActivity>(activityFactory, options ?? ActivityOptions.Default);

            if (!model.Validate(this))
                throw new Exception();

            if (!models.TryAdd(Activity.GetName<TActivity>(), model))
                throw new ArgumentException();
        }

        return this;
    }

    protected virtual bool ValidateActivityState<TState>()
        where TState : IAsyncStateMachine
        => true;

    bool IActivityStateValidator.Validate<TState>() => ValidateActivityState<TState>();

    protected abstract ValueTask ActivityStarted<TInput>(ActivityInstance instance, TInput input, ActivityOptions options)
        where TInput : class;

    ValueTask IActivityStartedCallback.InvokeAsync<TInput>(ActivityInstance instance, TInput input, ActivityOptions options)
        => ActivityStarted<TInput>(instance, input, options);

    internal protected abstract ValueTask ActivityCheckpointReachedAsync<TState>(ActivityInstance instance, TState executionState, TimeSpan remainingTime)
        where TState : IAsyncStateMachine;

    protected abstract ValueTask ActivityCompleted(ActivityInstance instance, Exception? e);

    // core method that launches activity and controls its execution
    private async Task ExecuteAsync(ActivityMetaModel model, IActivityStateProvider provider)
    {
        CancellationTokenSource? workflowTokenSource = null;
        try
        {
            var lockTaken = false;
            ActivityInstance instance = new(provider.InstanceName, model.Name);
            var activityState = default(IAsyncStateMachine);
            ActivityContext context;

            // prepare cancellation token
            if (provider.RemainingTime != InfiniteTimeSpan)
            {
                // attach timeout later
                workflowTokenSource = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);
            }

            // inform that the activity has been started
            try
            {
                await stateLock.AcquireAsync(lifecycleToken).ConfigureAwait(false);
                lockTaken = true;

                (activityState, context) = model.PrepareForExecution(in instance, provider, this, workflowTokenSource?.Token ?? lifecycleToken);

                if (!instances.TryAdd(instance, context))
                    throw new DuplicateWorkflowInstanceException(instance);

                if (provider is InitialActivityStateProvider initialStateProvider)
                    await initialStateProvider.InvokeCallback(this, instance).ConfigureAwait(false);

                await context.InitializeAsync().ConfigureAwait(false);
            }
            catch (DuplicateWorkflowInstanceException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (activityState is not null)
                    model.SetException(ref activityState, e);

                instances.Remove(instance);
                throw;
            }
            finally
            {
                if (lockTaken)
                    stateLock.Release();
            }

            // execute workflow
            workflowTokenSource?.CancelAfter(provider.RemainingTime); // attach timeout
            activityState.MoveNext();

            // finalize execution
            var error = default(ExceptionDispatchInfo);
            try
            {
                await context.ActivityTask.ConfigureAwait(false);
                await context.CleanupAsync().ConfigureAwait(false);

                switch (context.ExecutingActivity)
                {
                    case IAsyncDisposable disposable:
                        await disposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (OperationCanceledException) when (lifecycleToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException e) when (workflowTokenSource?.Token == e.CancellationToken)
            {
                var timeoutEx = new TimeoutException(null, e);

                if (e.StackTrace is { Length: > 0 } stackTrace)
                    ExceptionDispatchInfo.SetRemoteStackTrace(timeoutEx, stackTrace);

                error = ExceptionDispatchInfo.Capture(timeoutEx);
            }
            catch (Exception e)
            {
                error = ExceptionDispatchInfo.Capture(e);
            }

            // remove activity from the tracking list
            lockTaken = false;
            try
            {
                await stateLock.AcquireAsync(lifecycleToken).ConfigureAwait(false);
                lockTaken = true;

                instances.Remove(instance);
            }
            catch (ObjectDisposedException) when (lifecycleToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(lifecycleToken);
            }
            finally
            {
                if (lockTaken)
                    stateLock.Release();
            }

            // notify about activity completion
            await ActivityCompleted(instance, error?.SourceException).ConfigureAwait(false);
            error?.Throw();
        }
        finally
        {
            workflowTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Executes the specified activity.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TActivity"></typeparam>
    /// <param name="instanceName"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="DuplicateWorkflowInstanceException"><paramref name="instanceName"/> is already executing.</exception>
    public Task ExecuteAsync<TInput, TActivity>(string instanceName, TInput input)
        where TInput : class
        where TActivity : Activity<TInput>
    {
        if (instanceName is not { Length: > 0 })
            return Task.FromException(new ArgumentNullException(nameof(instanceName)));

        if (input is null)
            return Task.FromException(new ArgumentNullException(nameof(input)));

        if (!models.TryGetValue(Activity.GetName<TActivity>(), out var model))
            return Task.FromException(new GenericArgumentException<TActivity>("", nameof(TInput)));

        return ExecuteAsync(model, new ActivityMetaModel<TInput>.InitialActivityStateProvider(instanceName, input, model));
    }

    public Task ExecuteAsync<TActivity>(string instanceName)
        where TActivity : Activity<Missing>
        => ExecuteAsync<Missing, TActivity>(instanceName, Missing.Value);

    protected Task ExecuteAsync(string activityName, IActivityStateProvider provider)
    {
        if (activityName is not { Length: > 0 })
            return Task.FromException(new ArgumentNullException(nameof(activityName)));

        if (provider is null)
            return Task.FromException(new ArgumentNullException(nameof(provider)));

        if (!models.TryGetValue(activityName, out var model))
            return Task.FromException(new ArgumentException());

        return ExecuteAsync(model, provider);
    }

    /// <summary>
    /// Stops all workflows executed locally by this engine.
    /// </summary>
    /// <returns>The task representing asynchronous result.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        lifecycleSource.Cancel();
        await stateLock.AcquireAsync().ConfigureAwait(false);
        try
        {
            foreach (var activity in instances.Values)
            {
                try
                {
                    await activity.ActivityTask.ConfigureAwait(false);
                }
                catch
                {
                    // suspend any exception
                }
            }
        }
        finally
        {
            instances.Clear();
            stateLock.Dispose();
            lifecycleSource.Dispose();
        }
    }

    public ValueTask DisposeAsync() => suspending.FalseToTrue() ? DisposeAsyncCore() : ValueTask.CompletedTask;
}