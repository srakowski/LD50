namespace LD50
{
    using LD50.LdjamApi;
    using OneOf;
    using OneOf.Types;
    using Orleans;
    using Orleans.Runtime;

    public record VerificationCode(string Value, int Passcode)
    {
        public static VerificationCode Generate(int passCode)
        {
            var value = "ludum dare";
            return new VerificationCode(value, passCode);
        }
    }

    public abstract record InitVerifyResult;
    public sealed record AwaitingVerification(VerificationCode Code) : InitVerifyResult;
    public sealed record NotEligible : InitVerifyResult;

    public abstract record VerifyResult;
    public sealed record Verified : VerifyResult;
    public sealed record Unverified : VerifyResult;

    public interface IVerifyJammerAccount : IGrainWithStringKey
    {
        public Task<InitVerifyResult> Init(int passcode);
        public Task<VerifyResult> Verify(int passcode, string password);
    }

    public class VerifyJammerAccountState
    {
        public bool IsEligible { get; set; }
        public DateTime LastEligibilityCheckAtUtc { get; set; }
        public DateTime? VerificationStartedAtUtc { get; set; }
        public VerificationCode? VerificationCode { get; set; }
        public bool IsVerified { get; set; } = false;
    }

    public class VerifyJammerAccount : Grain, IVerifyJammerAccount
    {
        public const long MIN_LDJAM_PARTICIPANT = 46;
        public const long VERIFICATION_THREAD_ID = 284610;

        private readonly IPersistentState<VerifyJammerAccountState> _state;
        private readonly ILdjamApiClient _ldjamApiClient;
        private string _jammerName = null!;

        public VerifyJammerAccount(
            [PersistentState(nameof(VerifyJammerAccountState), "default")]
             IPersistentState<VerifyJammerAccountState> state,
            ILdjamApiClient ldjamApiClient)
        {
            _state = state;
            _ldjamApiClient = ldjamApiClient;
        }

        public override Task OnActivateAsync()
        {
            _jammerName = this.GetPrimaryKeyString();
            return Task.CompletedTask;
        }

        public async Task<InitVerifyResult> Init(int passcode)
        {
            if (!_state.State.IsEligible &&
                _state.State.LastEligibilityCheckAtUtc < DateTime.UtcNow.AddMinutes(-3))
            {
                await RecheckEligibility();
                _state.State.LastEligibilityCheckAtUtc = DateTime.UtcNow;
                await _state.WriteStateAsync();
            }

            if (!_state.State.IsEligible)
            {
                return new NotEligible();
            }

            var startedAt = _state.State.VerificationStartedAtUtc;
            if (!startedAt.HasValue || (DateTime.UtcNow - startedAt.Value).TotalMinutes > 10)
            {
                _state.State.VerificationStartedAtUtc = DateTime.UtcNow;
                _state.State.VerificationCode = VerificationCode.Generate(passcode);
                await _state.WriteStateAsync();
            }

            return new AwaitingVerification(_state.State.VerificationCode!);
        }

        public async Task<VerifyResult> Verify(int passcode, string password)
        {
            if (_state.State.IsVerified)
            {
                return new Verified();
            }

            var startedAt = _state.State.VerificationStartedAtUtc;
            if (!startedAt.HasValue
                || (DateTime.UtcNow - startedAt.Value).TotalMinutes > 10 
                || !_state.State.IsEligible
                || _state.State.VerificationCode == null 
                || _state.State.VerificationCode.Passcode != passcode)
            {
                return new Unverified();
            }

            var code = _state.State.VerificationCode;
            var result = await DoVerification(code);
            if (result.Value is False)
            {
                return new Unverified();
            }

            var ldjamUserId = (long)result.Value;           
            var jammerAccount = GrainFactory.GetGrain<IJammerAccount>(_jammerName);
            await jammerAccount.SetIsVerified(ldjamUserId, password);

            _state.State.IsVerified = true;
            await _state.WriteStateAsync();

            return new Verified();
        }

        private async Task RecheckEligibility()
        {
            try
            {
                var user = await _ldjamApiClient.GetUser(_jammerName);
                if (user.status != 200 || user.node_id == LdjamUser.NotFoundNodeId)
                    return;

                var gameList = await _ldjamApiClient.GetGameList(user.node_id);
                if (gameList.status != 200)
                    return;

                var latestGame = gameList.feed.OrderByDescending(f => f.id).First();
                var game = await _ldjamApiClient.GetGame(latestGame.id);
                if (game.status != 200)
                    return;

                var gameNode = game.node.First();
                var lastLdjam = gameNode.path.Split("/").Skip(3).First();
                var lastSubmit = int.Parse(lastLdjam);
                if (lastSubmit >= MIN_LDJAM_PARTICIPANT)
                {
                    _state.State.IsEligible = true;
                    await _state.WriteStateAsync();
                }
            }
            catch { }
        }

        private async Task<OneOf<long, False>> DoVerification(VerificationCode code)
        {
            try
            {
                var user = await _ldjamApiClient.GetUser(_jammerName);
                if (user.status != 200 || user.node_id == LdjamUser.NotFoundNodeId)
                    return new False();

                var ldjamUserId = user.node_id;

                var verificationThread = GrainFactory.GetGrain<IVerificationThread>(VERIFICATION_THREAD_ID);
                var isVerified = await verificationThread.VerifyCodeEntered(ldjamUserId, code.Value);

                if (isVerified) return ldjamUserId;
                else return new False();
            }
            catch 
            {
                return new False();
            }
        }
    }
}
