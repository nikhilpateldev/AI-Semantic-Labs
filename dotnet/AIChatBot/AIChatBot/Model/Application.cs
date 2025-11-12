using System.ComponentModel.DataAnnotations;

namespace AIChatBot.Model
{
    public class Application
    {
        [Key]
        public int ApplicationId { get; set; }
        public string? Status { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
