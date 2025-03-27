using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using System.IO;

namespace EntBossHP
{
    public class BossHpCounter : BasePlugin
    {
        public override string ModuleName => "BossHpCounter";
        public override string ModuleVersion => "1.0.0";
        public override string ModuleAuthor => "Rezki";

        private readonly Dictionary<CCSPlayerController, float> ClientLastShootHitBox = new();
        private readonly Dictionary<CCSPlayerController, CEntityInstance?> ClientEntityHit = new();
        private readonly Dictionary<CCSPlayerController, string?> ClientEntityNameHit = new();
        private float TimeWatch;
        private float LastForceShowBossHP;

        public override void Load(bool hotReload)
        {
            HookEntityOutput("math_counter", "OutValue", CounterOut);
            HookEntityOutput("func_physbox_multiplayer", "OnDamaged", BreakableOut);
            HookEntityOutput("func_physbox", "OnHealthChanged", BreakableOut);
            HookEntityOutput("func_breakable", "OnHealthChanged", BreakableOut);
            HookEntityOutput("prop_dynamic", "OnHealthChanged", Hitbox_Hook);

            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            if (@event.Userid.IsBot || @event.Userid.IsHLTV) return HookResult.Continue;

            ClientLastShootHitBox[@event.Userid] = 0.0f;
            ClientEntityHit[@event.Userid] = null;
            ClientEntityNameHit[@event.Userid] = null;

            return HookResult.Continue;
        }

        private void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);
            if (client?.IsBot == true || client?.IsHLTV == true) return;

            ClientLastShootHitBox.Remove(client!);
            ClientEntityHit.Remove(client!);
            ClientEntityNameHit.Remove(client!);
        }

        private unsafe float GetMathCounterValue(nint handle)
        {
            var offset = Schema.GetSchemaOffset("CMathCounter", "m_OutValue");
            return *(float*)IntPtr.Add(handle, offset + 24);
        }

        private HookResult CounterOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            if (!caller.IsValid || caller.DesignerName != "math_counter") return HookResult.Continue;

            var entityname = caller.Entity.Name;
            var hp = GetMathCounterValue(caller.Handle);

            var player = GetPlayer(activator);
            if (player == null || ClientLastShootHitBox[player] <= Server.EngineTime - 0.2f) return HookResult.Continue;

            ClientEntityHit[player] = caller;
            ClientEntityNameHit[player] = entityname;
            Print_BHUD(player, caller, entityname, (int)Math.Round(hp));

            return HookResult.Continue;
        }

        private HookResult BreakableOut(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            var player = GetPlayer(activator);
            if (player == null) return HookResult.Continue;

            ClientLastShootHitBox[player] = (float)Server.EngineTime;
            return HookResult.Continue;
        }

        private HookResult Hitbox_Hook(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
        {
            var player = GetPlayer(activator);
            if (player == null || !caller.IsValid) return HookResult.Continue;

            var entityname = caller.Entity.Name ?? "HP";
            var prop = new CBreakable(caller.Handle);

            if (!prop.IsValid || prop.Health > 500000) return HookResult.Continue;

            ClientEntityHit[player] = caller;
            ClientEntityNameHit[player] = entityname;
            Print_BHUD(player, caller, entityname, prop.Health > 0 ? prop.Health : 0);

            ClientLastShootHitBox[player] = (float)Server.EngineTime;
            return HookResult.Continue;
        }

        private void Print_BHUD(CCSPlayerController client, CEntityInstance entity, string name, int hp)
        {
            TimeWatch = (float)Server.EngineTime;
            if (!(ClientLastShootHitBox[client] > TimeWatch - 3.0f && LastForceShowBossHP + 0.1f < TimeWatch) && hp != 0) return;

            var players = Utilities.GetPlayers().Where(p => p.Team == CsTeam.CounterTerrorist).ToList();
            int activePlayers = players.Count(p => ClientLastShootHitBox[p] > TimeWatch - 7.0 && ClientEntityHit[p] == entity && name == ClientEntityNameHit[p]);

            // Menentukan jumlah bar berdasarkan HP
            int barCount = hp >= 1000 ? 10 :
                           hp >= 900 ? 9 :
                           hp >= 800 ? 8 :
                           hp >= 700 ? 7 :
                           hp >= 600 ? 6 :
                           hp >= 500 ? 5 :
                           hp >= 400 ? 4 :
                           hp >= 300 ? 3 :
                           hp >= 200 ? 2 :
                           hp >= 100 ? 1 : 0;

            string healthBar = new string('▯', barCount);
            string displayText = barCount > 0 ? $"{name}: {hp}\n{healthBar}" : $"{name}: {hp}";

            if (activePlayers > players.Count / 2)
            {
                players.ForEach(p => p.PrintToCenter(displayText));
            }
            else
            {
                players.Where(p => ClientLastShootHitBox[p] > TimeWatch - 7.0f && ClientEntityHit[p] == entity && name == ClientEntityNameHit[p])
                       .ToList().ForEach(p => p.PrintToCenter(displayText));
            }
            LastForceShowBossHP = TimeWatch;
        }



        private static CCSPlayerController? GetPlayer(CEntityInstance? instance)
        {
            if (instance?.DesignerName != "player") return null;
            var playerPawn = Utilities.GetEntityFromIndex<CCSPlayerPawn>((int)instance.Index);
            return playerPawn?.OriginalController?.Value;
        }
    }
}
