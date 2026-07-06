namespace VoiceMaster.DataClasses
{
    /// <summary>
    /// Resolved TTS parameters for a single Inworld AI request.
    /// Produced by ImmersionEngine.Resolve() from an NpcVoiceProfile + EmotionState.
    /// </summary>
    public readonly struct InworldTtsParams
    {
        public double SpeakingRate { get; init; }
        public double Temperature { get; init; }
        public string ModelId { get; init; }
        /// <summary>Bracketed TTS-2 steering instruction to prepend to the text, or "" for none.</summary>
        public string SteeringPrefix { get; init; }

        public InworldTtsParams(double speakingRate, double temperature, string modelId, string steeringPrefix = "")
        {
            SpeakingRate = speakingRate;
            Temperature = temperature;
            ModelId = modelId;
            SteeringPrefix = steeringPrefix ?? "";
        }
    }
}
