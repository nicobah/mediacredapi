using Swashbuckle.AspNetCore.Annotations;

namespace MediaCred.Models
{
    public class Evidence
    {
        [SwaggerSchema(ReadOnly = true)]
        public string? ID { get; set; }

        public string Name { get; set; }
    }
}
