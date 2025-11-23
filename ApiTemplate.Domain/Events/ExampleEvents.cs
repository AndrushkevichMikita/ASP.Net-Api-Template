namespace ApiTemplate.Domain.Events
{
    [KafkaTopic("queue.apitemplate.account_created")]
    public class AccountCreatedEvent : IntegrationEvent
    {
        public AccountCreatedEvent(int accountId, string email, string firstName, string lastName)
            : base()
        {
            AccountId = accountId;
            Email = email;
            FirstName = firstName;
            LastName = lastName;
        }

        public int AccountId { get; set; }

        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }

    [KafkaTopic("queue.apitemplate.account_updated")]
    public class AccountUpdatedEvent : IntegrationEvent
    {
        public AccountUpdatedEvent(int accountId, string email)
            : base()
        {
            AccountId = accountId;
            Email = email;
        }

        public int AccountId { get; set; }

        public string Email { get; set; }
    }
}

