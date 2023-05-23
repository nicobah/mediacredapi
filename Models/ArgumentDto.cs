using Swashbuckle.AspNetCore.Annotations;

namespace MediaCred.Models
{
    public class ArgumentDto
    {
        [SwaggerSchema(ReadOnly = true)]
        public string? ID { get; set; }

        public string? Claim { get; set; }

        public string? Ground { get; set; }

        public string? Warrant { get; set; }

        public string? artLink { get; set; }
    }
}
