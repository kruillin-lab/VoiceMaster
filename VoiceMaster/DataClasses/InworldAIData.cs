using System;

namespace VoiceMaster.DataClasses
{
    public class InworldAIData
    {
        public string ApiKey = "";
        public string ApiSecret = "";
        public string WorkspaceId = "";
        public string CharacterId = ""; // Default character if none specified
        public bool Enabled = false;
        
        public override string ToString()
        {
            return $"ApiKey: {(!string.IsNullOrEmpty(ApiKey) ? "***" : "empty")}, WorkspaceId: {WorkspaceId}";
        }
    }
}
