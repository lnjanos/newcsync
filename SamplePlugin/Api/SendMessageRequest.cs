using System;
using System.Runtime.CompilerServices;

namespace CrystalSync.Api
{
    // Token: 0x0200000A RID: 10

    public class SendMessageRequest
    {
        public string token { get; set; } = string.Empty;

        public string sender_type { get; set; } = string.Empty;

        public string sender_id { get; set; } = string.Empty;

        public string receiver_id { get; set; } = string.Empty;

        public string content { get; set; } = string.Empty;

        public string color { get; set; } = string.Empty;
    }
}
