using Robust.Shared.Serialization;
using Robust.Shared.Network;
using Robust.Shared.GameObjects;

namespace Content.Shared.MassMedia.Systems;

public abstract class SharedNewsSystem : EntitySystem
{
    public const int MaxTitleLength = 25;
    public const int MaxContentLength = 2048;
    // DS14
    public const int MaxCommentLength = 512;
    // DS14
    public const int MaxComments = 50;
    // DS14
    public static readonly TimeSpan CommentCooldown = TimeSpan.FromSeconds(30);
}

// DS14-start
[Serializable, NetSerializable]
public struct NewsComment
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string Content;

    [ViewVariables(VVAccess.ReadWrite)]
    public string? Author;

    [ViewVariables]
    public TimeSpan CommentTime;

    public NewsComment(string content, string? author, TimeSpan commentTime)
    {
        Content = content;
        Author = author;
        CommentTime = commentTime;
    }
}
// DS14-end

[Serializable, NetSerializable]
public struct NewsArticle
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string Title;

    [ViewVariables(VVAccess.ReadWrite)]
    public string Content;

    [ViewVariables(VVAccess.ReadWrite)]
    public string? Author;

    [ViewVariables]
    public ICollection<(NetEntity, uint)>? AuthorStationRecordKeyIds;

    [ViewVariables]
    public TimeSpan ShareTime;

    // DS14
    [ViewVariables(VVAccess.ReadWrite)]
    public int Likes;

    // DS14
    [ViewVariables(VVAccess.ReadWrite)]
    public int Dislikes;

    // DS14
    [ViewVariables(VVAccess.ReadWrite)]
    public List<NewsComment> Comments;

    // DS14
    [NonSerialized]
    [ViewVariables(VVAccess.ReadWrite)]
    public HashSet<NetUserId> LikedBy;

    // DS14
    [NonSerialized]
    [ViewVariables(VVAccess.ReadWrite)]
    public HashSet<NetUserId> DislikedBy;

    public NewsArticle(string title, string content, string? author, TimeSpan shareTime)
    {
        Title = title;
        Content = content;
        Author = author;
        AuthorStationRecordKeyIds = null;
        ShareTime = shareTime;
        Likes = 0;
        Dislikes = 0;
        Comments = new List<NewsComment>();
        LikedBy = new HashSet<NetUserId>();
        DislikedBy = new HashSet<NetUserId>();
    }
}

[ByRefEvent]
public record struct NewsArticlePublishedEvent(NewsArticle Article);

[ByRefEvent]
public record struct NewsArticleDeletedEvent;

// DS14: broadcast when a player successfully posts a comment under a news article.
[ByRefEvent]
public record struct NewsCommentPostedEvent(EntityUid Author, string Content);
