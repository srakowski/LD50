namespace LD50
{
    using LD50.LdjamApi;
    using Orleans;

    public interface IVerificationThread : IGrainWithIntegerKey
    {
        Task<bool> VerifyCodeEntered(long ldjamUserId, string code);
    }

    public class VerificationThread : Grain, IVerificationThread
    {
        private readonly ILdjamApiClient _ldjamApiClient;
        private long _threadId;
        private DateTime _lastRefreshAtUtc = DateTime.MinValue;
        private List<LdjamComment> _comments = new List<LdjamComment>();

        public VerificationThread(
            ILdjamApiClient ldjamApiClient)
        {
            _ldjamApiClient = ldjamApiClient;
        }

        public override Task OnActivateAsync()
        {
            _threadId = this.GetPrimaryKeyLong();
            return Task.CompletedTask;
        }

        public async Task<bool> VerifyCodeEntered(long ldjamUserId, string code)
        {
            if (_lastRefreshAtUtc < DateTime.UtcNow.AddSeconds(20))
            {
                await RefreshComments();
            }

            return _comments.Where(c => c.author == ldjamUserId).Any(c => c.body?.Contains(code) ?? false);
        }

        private async Task RefreshComments()
        {
            var comments = await _ldjamApiClient.GetThreadComments(_threadId);
            _comments = comments.comment.ToList();
            _lastRefreshAtUtc = DateTime.UtcNow;
        }
    }
}
