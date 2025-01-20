using System;
using System.Runtime.CompilerServices;

namespace CrystalSync.Api
{
    public class RegisterRequest
    {
        public string token { get; set; } = string.Empty;

        public string character_name { get; set; } = string.Empty;

        public string server_name { get; set; } = string.Empty;
    }
}
