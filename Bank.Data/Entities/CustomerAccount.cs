using System.ComponentModel.DataAnnotations;

namespace Bank.Data.Entities
{
    public class CustomerAccount
    {
        [Key]
        public int UserId { get; set; }
        
        public virtual List<Transaction>? Transactions { get; set; }

        public int CreatedById { get; set; }
        public DateTime CreatedOn { get; set; }
        public int ModifiedById { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
}
