using Bitcraft.Domain.Identity;

namespace Bitcraft.Domain.Common;

public abstract class BaseModel
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public Guid CreatedById { get; private set; }
    public AppUser? CreatedBy { get; private set; }
    public DateTime? ModifiedUtc { get; private set; }
    public Guid? ModifiedById { get; private set; }
    public AppUser? ModifiedBy { get; private set; }



    public void UpdateMetadata(Guid userId)
    {
        var utcNow = DateTime.UtcNow;
        if (CreatedById == Guid.Empty) SetCreator(userId, utcNow);
        SetModifier(userId, utcNow);
    }

    private void SetCreator(Guid userId, DateTime utcNow)
    {
        CreatedUtc = utcNow;
        CreatedById = userId;
    }

    private void SetModifier(Guid userId, DateTime utcNow)
    {
        ModifiedUtc = utcNow;
        ModifiedById = userId;
    }
}
