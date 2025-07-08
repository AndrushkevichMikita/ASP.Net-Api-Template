using ApiTemplate.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApiTemplate.Infrastructure
{
    public class ApplicationDbContext : IdentityDbContext<Account,
                                        IdentityRole<int>,
                                        int,
                                        IdentityUserClaim<int>,
                                        IdentityUserRole<int>,
                                        IdentityUserLogin<int>,
                                        IdentityRoleClaim<int>,
                                        AccountTokenEntity>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

#if DEBUG
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // for detect Multiple Collection Includes, to use AsSplitQuery()
            optionsBuilder.ConfigureWarnings(w => w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
            // logging the Command Execution
            // Enable here or in Elastic Apm, otherwice messages will duplicates
            //optionsBuilder.LogTo(message => Debug.WriteLine(message), LogLevel.Information)
            //              .EnableSensitiveDataLogging()
            //              .EnableDetailedErrors();
        }
#endif
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AccountTokenEntity>().HasOne(x => x.User).WithMany(x => x.Tokens).HasForeignKey(x => x.UserId);

            UseUtc(builder);
        }

        static void UseUtc(ModelBuilder builder)
        {
            // solution from https://stackoverflow.com/questions/4648540/entity-framework-datetime-and-utc
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc)); ;

            var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (entityType.IsKeyless)
                    continue;

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                        property.SetValueConverter(dateTimeConverter);
                    else if (property.ClrType == typeof(DateTime?))
                        property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }
    }
}