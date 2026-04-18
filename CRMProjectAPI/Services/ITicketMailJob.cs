namespace CRMProjectAPI.Services
{
    public interface ITicketMailJob
    {
        Task SendTicketClosedMailAsync(int ticketId, int customerId, byte status);
    }
}