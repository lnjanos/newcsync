using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace CrystalSync.Helpers
{
    internal class Utils
    {
        public static bool DecodeSender(SeString sender, XivChatType type, out Dictionary<string, uint> senderStruct)
        {
            //IL_004f: Unknown result type (might be due to invalid IL or missing references)
            //IL_0054: Unknown result type (might be due to invalid IL or missing references)
            if (sender == null)
            {
                senderStruct = null;
                return false;
            }
            foreach (Payload x in sender.Payloads)
            {
                PlayerPayload p = (PlayerPayload)(object)((x is PlayerPayload) ? x : null);
                if (p != null)
                {
                    senderStruct = new Dictionary<string, uint>();
                    senderStruct.Add(p.PlayerName, p.World.RowId);
                    return true;
                }
            }
            senderStruct = null;
            return false;
        }

        public static string Vector4ToHex(Vector4 color)
        {
            int r = (int)(color.X * 255f);
            int g = (int)(color.Y * 255f);
            int b = (int)(color.Z * 255f);
            int a = (int)(color.W * 255f);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }
    }
}
