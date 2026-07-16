using Hazel;
using System;

namespace WellsAntiCheat
{
    // Base class for a per-RPC validator. Each RPC we care about gets a subclass.
    internal class RpcCheck
    {
        public virtual bool Enabled { get; set; } = true;

        // Read the RPC payload and set blockRpc=true to discard it. Call Anticheat.Flag(...) to punish.
        public virtual void Validate(PlayerControl player, MessageReader reader, ref bool blockRpc) { }

        // If true, a non-host player sending this RPC is itself a violation.
        public virtual bool IsHostOnly() => false;

        // Which net object this RPC is expected to arrive on. Most are PlayerControl.
        public virtual Type GetExpectedNetObject() => typeof(PlayerControl);
    }
}
