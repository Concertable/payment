namespace Concertable.Payment.Domain;

public class ConcertPayeeEntity
{
    private ConcertPayeeEntity() { }

    private ConcertPayeeEntity(int concertId, Guid payeeUserId)
    {
        ConcertId = concertId;
        PayeeUserId = payeeUserId;
    }

    public int ConcertId { get; private set; }
    public Guid PayeeUserId { get; private set; }

    public static ConcertPayeeEntity Create(int concertId, Guid payeeUserId) => new(concertId, payeeUserId);

    public void Update(Guid payeeUserId)
    {
        PayeeUserId = payeeUserId;
    }
}
