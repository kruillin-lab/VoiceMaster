using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;

namespace VoiceMaster.DataClasses
{
    public class AddonTalkText
    {
        public string? Speaker { get; init; }

        public string? Text
        {
            get; init;
        }
    }
}
