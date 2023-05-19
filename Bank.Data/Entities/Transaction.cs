using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bank.Data.Entities
{
    public class Transaction
    {
        [Key]
        public Guid TransactionId { get; set; }
        public string? Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Account { get; set; } = string.Empty;
        public string? Description { get; set; }

        [ForeignKey("UserId")]
        public int UserId { get; set; }
        
    }
}
