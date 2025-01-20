using System;
using System.Runtime.CompilerServices;

namespace CrystalSync.Api
{
    public class RegisterResponse
    {
        public string discord_id { get; set; } = string.Empty;

        public string token { get; set; } = string.Empty;
    }

}
