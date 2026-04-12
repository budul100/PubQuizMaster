using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PubQuizMaster.Core.Services
{


    // ─────────────────────────────────────────────
    // SCORER SESSION SERVICE
    // Tracks which web clients are currently connected and maps them
    // to scorer assignments. Used by the SignalR hub.
    // Runtime only — not persisted.
    // ─────────────────────────────────────────────

    /// <summary>
    /// Manages active scorer connections (SignalR connection IDs → scorer IDs).
    /// Thread-safe. Used exclusively by the SignalR hub layer.
    /// </summary>
    public class SessionService
    {
        // connectionId → scorerId
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
            _connections = new();

        // scorerId → connectionId (reverse lookup)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
            _scorers = new();

        /// <summary>Registers a new SignalR connection for a scorer.</summary>
        public void RegisterConnection(string connectionId, string scorerId)
        {
            // Remove any stale connection for this scorer
            if (_scorers.TryGetValue(scorerId, out var oldConnection))
                _connections.TryRemove(oldConnection, out _);

            _connections[connectionId] = scorerId;
            _scorers[scorerId] = connectionId;
        }

        /// <summary>Removes a connection on disconnect.</summary>
        public void RemoveConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var scorerId))
                _scorers.TryRemove(scorerId, out _);
        }

        /// <summary>Returns the scorer ID for a connection ID, or null if unknown.</summary>
        public string? GetScorerId(string connectionId)
            => _connections.TryGetValue(connectionId, out var id) ? id : null;

        /// <summary>Returns the connection ID for a scorer ID, or null if not connected.</summary>
        public string? GetConnectionId(string scorerId)
            => _scorers.TryGetValue(scorerId, out var conn) ? conn : null;

        /// <summary>Returns all currently connected scorer IDs.</summary>
        public IEnumerable<string> ConnectedScorers => _scorers.Keys;

        /// <summary>Returns true if the given scorer currently has an active connection.</summary>
        public bool IsConnected(string scorerId) => _scorers.ContainsKey(scorerId);
    }
}