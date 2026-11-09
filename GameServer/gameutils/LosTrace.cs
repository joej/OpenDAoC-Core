using System;
using DOL.Logging;

namespace DOL.GS
{
    // Debug: compares the client's "target in view" flag, which player spells and range attacks use as their line of sight,
    // with the answer to a line-of-sight check (0xD0) for the same target, asked at the same moment.
    // Trace level on the logger "DOL.GS.LosTrace"; logconfig.xml has a disabled rule that writes it to its own file.
    // Each check sends one 0xD0 to the client, so this only runs while the logger is on.
    //
    // One tab-separated line per check, written when the answer arrives:
    //   <UTC time> <action> <player> <target> <flag 0|1> <check 0|1|timeout> <distance> <MATCH|MISMATCH|NOANSWER> <ms to answer>
    // An answer the handler still has cached (same pair, neither moved) comes back at once, with 0 ms.
    // Both values come from the client: this measures how often the flag disagrees with the check, it can't catch a
    // client that lies in both.
    public static class LosTrace
    {
        private static readonly Logger log = LoggerManager.Create("DOL.GS.LosTrace");

        public static void Compare(GamePlayer player, GameObject target, string action)
        {
            if (!log.IsTraceEnabled || player == null || target == null || target == player || target.ObjectID == 0)
                return;

            try
            {
                player.LosCheckHandler.StartLosCheck(player, target, new Listener(player, target, action, player.TargetInView));
            }
            catch (Exception e)
            {
                log.Trace($"{DateTime.UtcNow:O}\t{action}\t{player.Name}\t{target.Name}\terror: {e.Message}");
            }
        }

        private sealed class Listener : ILosCheckListener
        {
            private readonly GameObject _target;
            private readonly string _action;
            private readonly bool _flag;
            private readonly int _distance;
            private readonly long _start;

            public Listener(GamePlayer player, GameObject target, string action, bool flag)
            {
                _target = target;
                _action = action;
                _flag = flag;
                _distance = player.GetDistanceTo(target);
                _start = GameLoop.GameLoopTime;
            }

            public void HandleLosCheckResponse(GamePlayer player, LosCheckResponse response, ushort targetId)
            {
                string check = response switch
                {
                    LosCheckResponse.True => "1",
                    LosCheckResponse.False => "0",
                    _ => response.ToString().ToLowerInvariant()
                };
                string result = response switch
                {
                    LosCheckResponse.True => _flag ? "MATCH" : "MISMATCH",
                    LosCheckResponse.False => _flag ? "MISMATCH" : "MATCH",
                    _ => "NOANSWER"
                };
                log.Trace($"{DateTime.UtcNow:O}\t{_action}\t{player.Name}\t{_target.Name}\t{(_flag ? 1 : 0)}\t{check}\t{_distance}\t{result}\t{GameLoop.GameLoopTime - _start}");
            }
        }
    }
}
