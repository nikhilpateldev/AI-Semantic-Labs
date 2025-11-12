using System.ComponentModel.DataAnnotations;

namespace AIChatBot.Model
{
    public class Tenant
    {
        [Key]
        public int TenantId { get; set; }
        public string? Status { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
