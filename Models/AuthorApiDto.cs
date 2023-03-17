using Swashbuckle.AspNetCore.Annotations;

namespace MediaCred.Models
{
    public class AuthorApiDto
    {
        [SwaggerSchema(ReadOnly = true)]
        public string? ID { get; set; }
        public string Name { get; set; }
        public int? Age { get; set; }
        public string? Image { get; set; }
        public string? Bio { get; set; }
        public string? Company { get; set; }
        public string? Education { get; set; }
        public string? PoliticalOrientation { get; set; }
        public string? AreaOfExpertise { get; set; }
    }
}
