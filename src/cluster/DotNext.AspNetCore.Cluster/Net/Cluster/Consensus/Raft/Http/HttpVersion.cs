﻿using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    /// <summary>
    /// Represents supported HTTP version by Raft-over-HTTP implementation.
    /// </summary>
    [Serializable]
    public enum HttpVersion
    {
        /// <summary>
        /// Automatically selects HTTP version.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Use HTTP 1.1
        /// </summary>
        Http1,

        /// <summary>
        /// Use HTTP 2
        /// </summary>
        Http2,

        /// <summary>
        /// Use HTTP 3
        /// </summary>
        Http3,
    }
}
