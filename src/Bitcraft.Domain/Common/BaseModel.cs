using Bitcraft.Domain.Identity;

namespace Bitcraft.Domain.Common;

public abstract class BaseModel
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public Guid? CreatedById { get; private set; }
    public AppUser? CreatedBy { get; private set; }
    public DateTime? EditedUtc { get; private set; }
    public Guid? EditedById { get; private set; }
    public AppUser? EditedBy { get; private set; }



    public void UpdateMetadata(Guid? userId)
    {
        var utcNow = DateTime.UtcNow;
        if (CreatedById == null) SetCreator(userId, utcNow);
        SetEditor(userId, utcNow);
    }

    private void SetCreator(Guid? userId, DateTime utcNow)
    {
        CreatedUtc = utcNow;
        CreatedById = userId;
    }

    private void SetEditor(Guid? userId, DateTime utcNow)
    {
        EditedUtc = utcNow;
        EditedById = userId;
    }
}
