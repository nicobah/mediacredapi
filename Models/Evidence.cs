using Swashbuckle.AspNetCore.Annotations;

namespace MediaCred.Models
{
    public class Evidence : Node
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }
    }
}
