using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Enumerable = System.Linq.Enumerable;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using IO;

    public partial class CommandInterpreter
    {
        private interface IHandlerRegistry : IReadOnlyDictionary<int, CommandHandler>, IDataTransferObject.ITransformation<int>
        {
        }

        private sealed class SparseRegistry : Dictionary<int, CommandHandler>, IHandlerRegistry
        {
            internal SparseRegistry(IDictionary<int, CommandHandler> prototype)
                : base(prototype)
            {
            }

            async ValueTask<int> IDataTransferObject.ITransformation<int>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            {
                var id = await reader.ReadInt32Async(true, token).ConfigureAwait(false);
                if (!TryGetValue(id, out var interpreter))
                    throw new UnknownCommandException(id);
                await interpreter.InterpretAsync(reader, token).ConfigureAwait(false);
                return id;
            }
        }

        // this registry is optimized for situation when command IDs
        // are in range [0, X)
        private sealed class ContiguousRegistry : IHandlerRegistry
        {
            private readonly CommandHandler[] interpreters;

            internal ContiguousRegistry(IDictionary<int, CommandHandler> prototype)
            {
                interpreters = new CommandHandler[prototype.Count];
                for (var i = 0; i < interpreters.Length; i++)
                    interpreters[i] = prototype[i];
            }

            int IReadOnlyCollection<KeyValuePair<int, CommandHandler>>.Count => interpreters.Length;

            IEnumerable<int> IReadOnlyDictionary<int, CommandHandler>.Keys
                => Enumerable.Range(0, interpreters.Length);

            IEnumerable<CommandHandler> IReadOnlyDictionary<int, CommandHandler>.Values => interpreters;

            bool IReadOnlyDictionary<int, CommandHandler>.ContainsKey(int key)
                => key >= 0 && key < interpreters.Length;

            public CommandHandler this[int index] => interpreters[index];

            bool IReadOnlyDictionary<int, CommandHandler>.TryGetValue(int key, [MaybeNullWhen(false)] out CommandHandler value)
            {
                if (key >= 0 && key < interpreters.Length)
                {
                    value = interpreters[key];
                    return true;
                }

                value = null;
                return false;
            }

            async ValueTask<int> IDataTransferObject.ITransformation<int>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            {
                var id = await reader.ReadInt32Async(true, token).ConfigureAwait(false);
                if (id < 0 || id >= interpreters.Length)
                    throw new UnknownCommandException(id);

                await interpreters[id].InterpretAsync(reader, token).ConfigureAwait(false);
                return id;
            }

            public IEnumerator<KeyValuePair<int, CommandHandler>> GetEnumerator()
            {
                for (var i = 0; i < interpreters.Length; i++)
                    yield return new KeyValuePair<int, CommandHandler>(i, interpreters[i]);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static IHandlerRegistry CreateRegistry(IDictionary<int, CommandHandler> prototype)
        {
            // check whether the command IDs representing index in the array
            for (var i = 0; i < prototype.Count; i++)
            {
                if (!prototype.ContainsKey(i))
                    return new SparseRegistry(prototype);
            }

            return new ContiguousRegistry(prototype);
        }
    }
}