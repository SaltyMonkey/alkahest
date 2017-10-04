using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Alkahest.Core.IO;
using Alkahest.Core.Logging;

namespace Alkahest.Core.Net.Protocol
{
    public sealed class PacketProcessor
    {
        const BindingFlags CreateFlags =
            BindingFlags.DeclaredOnly |
            BindingFlags.NonPublic |
            BindingFlags.Static;

        static readonly Log _log = new Log(typeof(PacketProcessor));

        public PacketSerializer Serializer { get; }

        readonly HashSet<RawPacketHandler> _wildcardRawHandlers =
            new HashSet<RawPacketHandler>();

        readonly Dictionary<ushort, HashSet<RawPacketHandler>> _rawHandlers =
            new Dictionary<ushort, HashSet<RawPacketHandler>>();

        readonly Dictionary<ushort, HashSet<Delegate>> _highPriorityHandlers =
            new Dictionary<ushort, HashSet<Delegate>>();
        readonly Dictionary<ushort, HashSet<Delegate>> _lowPriorityHandlers =
            new Dictionary<ushort, HashSet<Delegate>>();
        readonly Dictionary<ushort, HashSet<Delegate>> _normalPriorityHandlers =
            new Dictionary<ushort, HashSet<Delegate>>();

        //can't keep readonly... sadness TODO: Fix it
        readonly Dictionary<ushort, HashSet<Delegate>> _handlers =
           new Dictionary<ushort, HashSet<Delegate>>();

        readonly IReadOnlyCollection<Delegate> _emptyHandlers =
            new List<Delegate>();

        readonly object _listLock = new object();

        readonly object _invokeLock = new object();

        public PacketProcessor(PacketSerializer serializer)
        {
            Serializer = serializer ??
                throw new ArgumentNullException(nameof(serializer));

            foreach (var code in serializer.Messages.Game.OpCodeToName.Keys)
            {
                _rawHandlers.Add(code, new HashSet<RawPacketHandler>());
                 _lowPriorityHandlers.Add(code, new HashSet<Delegate>());
                _highPriorityHandlers.Add(code, new HashSet<Delegate>());
                _normalPriorityHandlers.Add(code, new HashSet<Delegate>());
                _handlers.Add(code, new HashSet<Delegate>());
            }
        }

        ushort GetOpCode(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (!Serializer.Messages.Game.NameToOpCode.TryGetValue(name, out var op))
            {
                _log.Warning("Invalid opcode, partial table?");
                // throw new ArgumentException("Invalid opcode name.", nameof(name));
                return 0;
            }
            return op;
        }

        static string GetOpCodeName(Type t)
        {
            var name = t.GetMethod("Create", CreateFlags, null, Type.EmptyTypes,
                null)?.GetCustomAttribute<PacketAttribute>()?.OpCode;

            if (name == null)
                throw new ArgumentException("Invalid packet type.", "handler");

            return name;
        }

        public void AddRawHandler(RawPacketHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_listLock)
                _wildcardRawHandlers.Add(handler);
        }

        public void RemoveRawHandler(RawPacketHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_listLock)
                _wildcardRawHandlers.Remove(handler);
        }

        public void UpdatePrioritiesForHandlers()
        {
            foreach (var dictItem in _highPriorityHandlers)
            {
                _handlers[dictItem.Key].UnionWith(dictItem.Value);
            }
            foreach (var dictItem in _normalPriorityHandlers)
            {
                _handlers[dictItem.Key].UnionWith(dictItem.Value);
            }
            foreach (var dictItem in _lowPriorityHandlers)
            {
                _handlers[dictItem.Key].UnionWith(dictItem.Value);
            }
            _highPriorityHandlers.Clear();
            _normalPriorityHandlers.Clear();
            _lowPriorityHandlers.Clear();
        }

        public void AddRawHandler(string name, RawPacketHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var opCode = GetOpCode(name);
            if (opCode == 0)
                return;
            lock (_listLock)
                _rawHandlers[opCode].Add(handler);
        }

        public void RemoveRawHandler(string name, RawPacketHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var opCode = GetOpCode(name);
            if (opCode == 0)
                return;
            lock (_listLock)
                _rawHandlers[opCode].Remove(handler);
        }

        public void AddHandler<T>(PacketHandler<T> handler, HandlerPriority priority = HandlerPriority.Normal)
            where T : Packet
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var opCode = GetOpCode(GetOpCodeName(typeof(T)));
            if (opCode == 0)
                return;

            lock (_listLock)
                switch (priority)
                {
                    case HandlerPriority.Low:
                         _lowPriorityHandlers[opCode].Add(handler);
                        break;
                    case HandlerPriority.High:
                        _highPriorityHandlers[opCode].Add(handler);
                        break;
                    default:
                        _normalPriorityHandlers[opCode].Add(handler);
                      break;
                }
        }

        public void RemoveHandler<T>(PacketHandler<T> handler)
            where T : Packet
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var opCode = GetOpCode(GetOpCodeName(typeof(T)));
            if(opCode == 0)
                return;
            
            lock (_listLock)
                _handlers[opCode].Remove(handler);
        }

        internal static PacketHeader ReadHeader(byte[] buffer)
        {
            using (var reader = new TeraBinaryReader(buffer))
            {
                var length = (ushort)(reader.ReadUInt16() -
                    PacketHeader.HeaderSize);
                var opCode = reader.ReadUInt16();

                return new PacketHeader(length, opCode);
            }
        }

        internal static void WriteHeader(PacketHeader header, byte[] buffer)
        {
            using (var writer = new TeraBinaryWriter(buffer))
            {
                writer.WriteUInt16((ushort)(header.Length +
                    PacketHeader.HeaderSize));
                writer.WriteUInt16(header.OpCode);
            }
        }

        internal bool Process(GameClient client, Direction direction,
            ref PacketHeader header, ref byte[] payload)
        {
            var rawHandlers = new List<RawPacketHandler>();

            // Make a copy so we don't have to lock while iterating.
            lock (_listLock)
            {
                rawHandlers.AddRange(_wildcardRawHandlers);
                rawHandlers.AddRange(_rawHandlers[header.OpCode]);
            }

            var send = true;
            var name = Serializer.Messages.Game.OpCodeToName[header.OpCode];
            var original = payload;

            if (rawHandlers.Count != 0)
            {
                var packet = new RawPacket(name)
                {
                    Payload = payload.Slice(0, header.Length)
                };

                foreach (var handler in rawHandlers)
                {
                    try
                    {
                        lock (_invokeLock)
                            send &= handler(client, direction, packet);
                    }
                    catch (Exception e) when (!Debugger.IsAttached)
                    {
                        _log.Error("Unhandled exception in raw packet handler:");
                        _log.Error(e.ToString());
                    }
                }

                payload = packet.Payload;
                header = new PacketHeader((ushort)packet.Payload.Length, header.OpCode);
            }

            
            IReadOnlyCollection<Delegate> handlers = _handlers[header.OpCode];

            lock (_listLock)
                handlers = handlers.Count != 0 ? handlers.ToArray() : _emptyHandlers;

            if (handlers.Count != 0)
            {
                var packet = Serializer.Create(header.OpCode);
                var good = true;

                try
                {
                    Serializer.Deserialize(payload.Slice(0, header.Length), packet);
                }
                catch (EndOfStreamException)
                {
                    _log.Error("{0}: {1} failed to deserialize; skipping typed packet handlers",
                        direction.ToDirectionString(), name);
                    good = false;
                }

                if (good)
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            lock (_invokeLock)
                                send &= (bool)handler.DynamicInvoke(client,
                                    direction, packet);
                        }
                        catch (Exception e) when (!Debugger.IsAttached)
                        {
                            _log.Error("Unhandled exception in packet handler:");
                            _log.Error(e.ToString());
                        }
                    }

                    payload = Serializer.Serialize(packet);
                    header = new PacketHeader((ushort)payload.Length, header.OpCode);
                }
            }

            _log.Debug("{0}: {1} ({2} bytes{3})", direction.ToDirectionString(),
                name, header.Length, send ? string.Empty : ", discarded");

            if (send && payload.Length > PacketHeader.MaxPayloadSize)
            {
                _log.Error("{0}: {1} is too big ({2} bytes) to be sent correctly; sending original",
                    direction.ToDirectionString(), name, payload.Length);

                payload = original;
                header = new PacketHeader((ushort)payload.Length, header.OpCode);
            }

            return send;
        }
    }
}
