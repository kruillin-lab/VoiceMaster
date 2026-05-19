using VoiceMaster.DataClasses;
using VoiceMaster.Services;
using System.Threading.Tasks;

namespace VoiceMaster.Helper.Functional;

/// <summary>
/// Wrapper to adapt existing LipSyncHelper to ILipSyncHelper interface
/// </summary>
public class LipSyncHelperWrapper : ILipSyncHelper
{
    private readonly LipSyncHelper _lipSyncHelper;

    public LipSyncHelperWrapper(LipSyncHelper lipSyncHelper)
    {
        _lipSyncHelper = lipSyncHelper;
    }

    public Task TryLipSync(VoiceMessage message)
    {
        _lipSyncHelper.TryLipSync(message);
        return Task.CompletedTask;
    }
}
