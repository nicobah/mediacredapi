using MediaCred.Models;
using Microsoft.AspNetCore.SignalR;

namespace MediaCred
{
    public class Argument : Node
    {
        public string Claim { get; set; }

        public string? Ground { get; set; }

        public string? Warrant { get; set; }
        public bool IsValid { get; set; } = false;

        public double? Weight { get; set; } = 1;


        public override string GetFullString()
        {
            return "claim: " + this.Claim + " ground: " + this.Ground + " warrant: " + this.Warrant;
        }

        public override string ToString()
        {
            return this.Claim;
        }
    }
}
