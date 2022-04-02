namespace LD50;

using Orleans;
using Orleans.Runtime;

public abstract record AdoptionResult();
public sealed record AdoptionAccepted(long SpriteId) : AdoptionResult;
public sealed record AdoptionRejected(string reason) : AdoptionResult;

public interface IBlipManager : IGrainWithIntegerKey
{
    Task<AdoptionResult> AdoptASprite(string jammerName);
}

public class BlipManagerState
{
}

public class BlipManager : Grain, IBlipManager
{
    private readonly IPersistentState<BlipManagerState> _state;

    public BlipManager(
        [PersistentState(nameof(BlipManagerState), "default")]
        IPersistentState<BlipManagerState> state)
    {
        _state = state;
    }

    public Task<AdoptionResult> AdoptASprite(string jammerName)
    {
        throw new NotImplementedException();
    }
}