namespace LD50.LdjamApi;

using Refit;

public class LdjamUser
{
    public const long NotFoundNodeId = 2;
    public int status { get; set; }
    public long node_id { get; set; }
}

public class LdjamFeedItem
{
    public long id { get; set; }
}

public class LdjamGameList
{
    public int status { get; set; }
    public LdjamFeedItem[] feed { get; set; } = null!;
}

public class LdjamGameNode
{
    public string name { get; set; } = null!;
    public string path { get; set; } = null!;
}

public class LdjamGame
{
    public int status { get; set; }
    public LdjamGameNode[] node { get; set; } = null!;
}

public class LdjamComment
{
    public long author { get; set; }
    public string body { get; set; } = null!;
}

public class LdjamThreadComments
{
    public int status { get; set; }
    public LdjamComment[] comment { get; set; } = null!;
}

public interface ILdjamApiClient
{
    [Get("/vx/node2/walk/1/users/{ldjamUserName}")]
    public Task<LdjamUser> GetUser(string ldjamUserName);

    [Get("/vx/node/feed/{nodeId}/authors/item/game")]
    public Task<LdjamGameList> GetGameList(long nodeId);

    [Get("/vx/node2/get/{nodeId}")]
    public Task<LdjamGame> GetGame(long nodeId);

    [Get("/vx/comment/getbynode/{nodeId}")]
    public Task<LdjamThreadComments> GetThreadComments(long nodeId);
}
