using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using Missing = System.Reflection.Missing;
using static System.Threading.Timeout;
using ExceptionDispatchInfo = System.Runtime.ExceptionServices.ExceptionDispatchInfo;

namespace DotNext.Workflow;

using Threading;

/// <summary>
/// Represents workflow execution engine.
/// </summary>
public abstract class WorkflowEngine : ICheckpointCallback, IAsyncDisposable
{
    private readonly struct RunningActivity : IDisposable
    {
        private readonly CancellationTokenSource? source;
        internal readonly Task ActivityTask;

        internal RunningActivity(ActivityBuilder builder, TimeSpan timeout, CancellationToken lifecycleToken)
        {
            ActivityTask = builder.As<TaskCompletionSource>().Task;

            if (timeout == InfiniteTimeSpan)
            {
                Token = lifecycleToken;
            }
            else
            {
                source = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);
                source.CancelAfter(timeout);
                Token = source.Token;
            }
        }

        internal bool CanTimeout => source is not null;

        internal CancellationToken Token { get; }

        public void Dispose() => source?.Dispose();
    }

    private readonly Dictionary<Type, ActivityMetaModel> models;
    private readonly Dictionary<ActivityInstance, RunningActivity> instances;
    private readonly AsyncExclusiveLock stateLock;
    private readonly CancellationTokenSource lifecycleSource;
    private readonly CancellationToken lifecycleToken; // cached to avoid ObjectDisposedException
    private AtomicBoolean suspending;

    /// <summary>
    /// Initializes a new workflow engine.
    /// </summary>
    protected WorkflowEngine()
    {
        models = new Dictionary<Type, ActivityMetaModel>();
        stateLock = new();
        lifecycleSource = new();
        lifecycleToken = lifecycleSource.Token;
        instances = new();
    }

    /// <summary>
    /// Registers 
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TActivity"></typeparam>
    /// <param name="activityFactory"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public WorkflowEngine RegisterActivity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TInput, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TActivity>(Func<TActivity> activityFactory, ActivityOptions? options = null)
        where TInput : class
        where TActivity : Activity<TInput>
    {
        ArgumentNullException.ThrowIfNull(activityFactory);

        var model = new ActivityMetaModel(activityFactory, options ?? ActivityOptions.Default);
        return models.TryAdd(typeof(TActivity), model) ? this : throw new ArgumentException();
    }

    protected abstract Task ActivityStarted<TInput>(ActivityInstance instance, TInput input, ActivityOptions options)
        where TInput : class;

    protected abstract Task ActivityCheckpointReachedAsync<TState>(ActivityInstance instance, TState executionState, TimeSpan remainingTime)
        where TState : IAsyncStateMachine;

    Task ICheckpointCallback.CheckpointReachedAsync<TState>(ActivityInstance instance, TState executionState, TimeSpan remainingTime)
        => ActivityCheckpointReachedAsync(instance, executionState, remainingTime);

    protected abstract Task ActivityCompleted(ActivityInstance instance, Exception? e);

    private async Task ExecuteAsync<TInput>(string instanceName, TInput input, ActivityBuilder builder, IAsyncStateMachine activityState, TimeSpan timeout, bool notifyStarted)
        where TInput : class
    {
        var lockTaken = false;
        ActivityInstance instance;
        var activity = default(RunningActivity);

        // inform that the activity has been started
        try
        {
            await stateLock.AcquireAsync(lifecycleToken).ConfigureAwait(false);
            lockTaken = true;

            instance = new(instanceName, builder.ActivityName);
            activity = new(builder, timeout, lifecycleToken);

            if (!instances.TryAdd(instance, activity))
            {
                activity.Dispose();
                throw new DuplicateWorkflowInstanceException(instance);
            }

            if (notifyStarted)
                await ActivityStarted(instance, input, builder.Options).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            builder.SetException(e);
            activity.Dispose();
            throw;
        }
        finally
        {
            if (lockTaken)
                stateLock.Release();
        }

        // execute workflow
        activityState.MoveNext();

        // finalize execution
        var error = default(ExceptionDispatchInfo);
        try
        {
            await builder.As<TaskCompletionSource>().Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifecycleToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException e) when (activity.CanTimeout && e.CancellationToken == activity.Token)
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
        finally
        {
            activity.Dispose();
        }

        await ActivityCompleted(instance, error?.SourceException).ConfigureAwait(false);

        // remove activity from the tracking list
        lockTaken = false;
        try
        {
            await stateLock.AcquireAsync(lifecycleToken).ConfigureAwait(false);
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

        error?.Throw();
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
        if (input is null)
            return Task.FromException(new ArgumentNullException(nameof(input)));

        if (!models.TryGetValue(typeof(TActivity), out var model))
            return Task.FromException(new GenericArgumentException<TActivity>("", nameof(TInput)));

        var activity = model.ActivityFactory() as TActivity;
        Debug.Assert(activity is not null);

        var builder = new ActivityBuilder(this, model);
        var activityState = model.CreateStateMachine(
            ActivityBuilder.InitialState,
            builder,
            new ActivityContext<TInput>(instanceName, model.Options.Timeout, activity),
            activity,
            CancellationToken.None
        );

        return ExecuteAsync(instanceName, input, builder, activityState, model.Options.Timeout, notifyStarted: true);
    }

    public Task ExecuteAsync<TActivity>(string instanceName)
        where TActivity : Activity<Missing>
        => ExecuteAsync<Missing, TActivity>(instanceName, Missing.Value);

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
                finally
                {
                    activity.Dispose();
                }
            }
        }
        finally
        {
            stateLock.Dispose();
            instances.Clear();
            lifecycleSource.Dispose();
        }
    }

    public ValueTask DisposeAsync() => suspending.FalseToTrue() ? DisposeAsyncCore() : ValueTask.CompletedTask;
}