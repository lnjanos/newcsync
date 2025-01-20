using System;
using System.Runtime.CompilerServices;

namespace CrystalSync.Api
{
    // Token: 0x0200000B RID: 11
    public class SendEmoteRequest
    {
        public string token { get; set; } = string.Empty;

        public string sender_type { get; set; } = string.Empty;

        public string sender_id { get; set; } = string.Empty;

        public string receiver_id { get; set; } = string.Empty;

        public string content { get; set; } = string.Empty;

        public string emote_command { get; set; } = string.Empty;

        public string color { get; set; } = string.Empty;
    }
}
