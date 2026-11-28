using Content.Server.Backmen.GameTicking.Rules.Components;

namespace Content.Server.Backmen.Blob;

public static class BlobSilence
{
    public static bool BeforeAnnouncement(IEntityManager entityManager)
    {
        var query = entityManager.EntityQueryEnumerator<BlobRuleComponent>();
        while (query.MoveNext(out _, out var rule))
        {
            if (rule.Stage == BlobStage.Default)
                return true;
        }

        return false;
    }
}
