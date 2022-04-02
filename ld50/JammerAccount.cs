namespace LD50;
using Orleans;
using Orleans.Runtime;
using System.Security.Cryptography;
using System.Text;

public interface IJammerAccount : IGrainWithStringKey
{
    Task<bool> GetIsVerified();
    Task SetIsVerified(long ldjamUserId, string password);
    Task<bool> PasswordMatches(string password);
    Task<AdoptionResult> AdoptASprite();
}

public class JammerAccountState
{
    public bool IsVerified { get; set; } = false;
    public long LdjamUserId { get; set; } = 0;
    public string EncryptedPassword { get; set; } = null!;
    public string EncryptedToken { get; set; } = null!;
    public DateTime EncryptedTokenCreatedAtUtc { get; set; } = DateTime.MinValue;
    public List<long> AdoptedSprites { get; init; } = new();
}

public class JammerAccount : Grain, IJammerAccount
{
    private readonly IPersistentState<JammerAccountState> _state;
    private string _jammerName = null!;

    public JammerAccount(
        [PersistentState(nameof(JammerAccountState), "default")]
        IPersistentState<JammerAccountState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync()
    {
        _jammerName = this.GetPrimaryKeyString();
        return Task.CompletedTask;
    }

    public Task<bool> GetIsVerified()
    {
        return Task.FromResult(_state.State.IsVerified);
    }

    public async Task SetIsVerified(long ldjamUserId, string password)
    {
        _state.State.IsVerified = true;
        _state.State.LdjamUserId = ldjamUserId;
        _state.State.EncryptedPassword = Encrypt(password);
        await _state.WriteStateAsync();
    }

    public async Task<bool> PasswordMatches(string password)
    {
        await Task.CompletedTask;
        return _state.State.EncryptedPassword == Encrypt(password);
    }

    public async Task<AdoptionResult> AdoptASprite()
    {
        if (_state.State.IsVerified)
        {
            return new AdoptionRejected("Requesting account is not verified.");
        }

        var caretaker = GrainFactory.GetGrain<IBlipManager>(0);
        
        var adoptionResult = await caretaker.AdoptASprite(_jammerName);
        if (adoptionResult is AdoptionAccepted accepted)
        {
            _state.State.AdoptedSprites.Add(accepted.SpriteId);
            await _state.WriteStateAsync();
        }
        return adoptionResult;
    }

    private string Encrypt(string value)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.ASCII.GetBytes(value)));
    }
}
