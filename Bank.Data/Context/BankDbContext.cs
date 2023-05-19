
using Bank.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;

namespace Bank.Data.Context
{
    public class BankDbContext : DbContext
    {
        IHttpContextAccessor _context;
        public DbSet<CustomerAccount> CustomerAccount { get; set; }
        public DbSet<Transaction> Transaction { get; set; }

        public BankDbContext(DbContextOptions<BankDbContext> options, IHttpContextAccessor context)
            : base(options)
        {
            _context = context;
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SetDefaultValues();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            SetDefaultValues();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void SetDefaultValues()
        {
            var entities = ChangeTracker.Entries().Where(x => x.State == EntityState.Added || x.State == EntityState.Modified);
            int? id = 0;

            if  (_context.HttpContext != null)
            {
                var user = _context.HttpContext.User;

                if (user != null)
                {
                    var claims = user.Identities.First().Claims;
                    // The bearer token for the unique userid
                    //id = claims.Where(x => x.Type == "xxxx").Select(s => s.Value).SingleOrDefault();
                }

                
                foreach (var entity in entities)
                {
                    if (entity.State == EntityState.Added)
                    {
                        TrySetProperty(entity.Entity, "CreatedById", id);
                        TrySetProperty(entity.Entity, "CreatedOn", DateTime.UtcNow);
                    }

                    TrySetProperty(entity.Entity, "ModifiedById", id);
                    TrySetProperty(entity.Entity, "ModifiedOn", DateTime.UtcNow);
                }
            }
        }

        private void TrySetProperty(object obj, string property, object value)
        {
            var prop = obj.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
                prop.SetValue(obj, value, null);
        }
    }
}
