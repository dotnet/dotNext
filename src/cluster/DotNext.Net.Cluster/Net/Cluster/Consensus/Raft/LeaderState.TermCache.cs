using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal partial class LeaderState<TMember>
{
    // Splay Tree is an ideal candidate for caching terms because
    // 1. When all members are online, their indexes within WAL is in sync, so the necessary term is the root of the tree
    // with O(1) access
    // 2. Removing obsolete indexes is O(1)
    // 3. Cache cleanup is O(1)
    [StructLayout(LayoutKind.Auto)]
#if DEBUG
    internal
#else
    private
#endif
    struct TermCache
    {
        private TermCacheNode? root;
        private int addCount;

        internal readonly int ApproximatedCount => addCount;

        internal void Add(long index, long term)
        {
            TermCacheNode? parent = null;

            for (var x = root; x is not null; x = index < x.Index ? x.Left : x.Right)
            {
                parent = x;
            }

            TermCacheNode node;
            if (parent is null)
            {
                root = node = new(index, term);
                addCount = 1;
            }
            else
            {
                switch (index.CompareTo(parent.Index))
                {
                    case < 0:
                        parent.Left = node = new(index, term) { Parent = parent };
                        addCount += 1;
                        break;
                    case > 0:
                        parent.Right = node = new(index, term) { Parent = parent };
                        addCount += 1;
                        break;
                    case 0:
                        Debug.Assert(term == parent.Term);
                        node = parent;
                        break;
                }
            }

            Splay(node);
        }

        internal bool TryGet(long index, out long term)
        {
            var node = FindNode(index);
            if (node is not null)
            {
                term = node.Term;
                return true;
            }

            term = default;
            return false;
        }

        internal void RemovePriorTo(long index)
        {
            var node = FindNode(index);

            if (node is not null)
            {
                node.Left = null;

                if (node.Right is null)
                    addCount = 1;
            }
        }

        internal void Clear() => this = default;

        private TermCacheNode? FindNode(long index)
        {
            var result = root;
            while (result is not null)
            {
                switch (index.CompareTo(result.Index))
                {
                    case < 0:
                        result = result.Left;
                        continue;
                    case > 0:
                        result = result.Right;
                        continue;
                    case 0:
                        Splay(result);
                        goto exit;
                }
            }

        exit:
            return result;
        }

        private void RotateRight(TermCacheNode node)
        {
            Debug.Assert(node.Left is not null);

            var y = node.Left;
            node.Left = y.Right;

            if (y.Right is not null)
                y.Right.Parent = node;

            y.Parent = node.Parent;

            switch (node)
            {
                case { Parent: null }:
                    root = y;
                    break;
                case { IsRightNode: true }:
                    node.Parent.Right = y;
                    break;
                default:
                    node.Parent.Left = y;
                    break;
            }

            y.Right = node;
            node.Parent = y;
        }

        private void RotateLeft(TermCacheNode node)
        {
            Debug.Assert(node.Right is not null);

            var y = node.Right;
            node.Right = y.Left;

            if (y.Left is not null)
                y.Left.Parent = node;

            y.Parent = node.Parent;

            switch (node)
            {
                case { Parent: null }:
                    root = y;
                    break;
                case { IsLeftNode: true }:
                    node.Parent.Left = y;
                    break;
                default:
                    node.Parent.Right = y;
                    break;
            }

            y.Left = node;
            node.Parent = y;
        }

        private void Splay(TermCacheNode node)
        {
            while (node.Parent is not null)
            {
                switch (node)
                {
                    case { Parent: { Parent: null } }:
                        if (node.IsLeftNode)
                            RotateRight(node.Parent); // zig rotation
                        else
                            RotateLeft(node.Parent); // zag rotation
                        break;
                    case { IsLeftNode: true, Parent: { IsLeftNode: true } }:
                        // zig-zig rotation
                        RotateRight(node.Parent.Parent);
                        RotateRight(node.Parent);
                        break;
                    case { IsRightNode: true, Parent: { IsRightNode: true } }:
                        // zag-zag rotation
                        RotateLeft(node.Parent.Parent);
                        RotateLeft(node.Parent);
                        break;
                    case { IsRightNode: true, Parent: { IsLeftNode: true } }:
                        // zig-zag rotation
                        RotateLeft(node.Parent);
                        RotateRight(node.Parent);
                        break;
                    default:
                        // zag-zig rotation
                        RotateRight(node.Parent);
                        RotateLeft(node.Parent);
                        break;
                }
            }
        }
    }

    private sealed class TermCacheNode
    {
        internal readonly long Index, Term;
        internal TermCacheNode? Left, Right, Parent;

        internal TermCacheNode(long index, long term)
        {
            Index = index;
            Term = term;
        }

        [MemberNotNullWhen(true, nameof(Parent))]
        internal bool IsLeftNode => Parent?.Left == this;

        [MemberNotNullWhen(true, nameof(Parent))]
        internal bool IsRightNode => Parent?.Right == this;
    }

    private TermCache precedingTermCache;
}