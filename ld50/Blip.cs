namespace LD50;

using Orleans;
using Orleans.Runtime;

public interface IBlip : IGrainWithIntegerKey
{
}

public class BlipState
{
}

public class Blip
{
    private readonly IPersistentState<BlipState> _state;

    public Blip(
        [PersistentState(nameof(BlipState), "default")]
        IPersistentState<BlipState> state)
    {
        _state = state;
    }
}
