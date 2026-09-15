namespace Setus.HorrorFramework.Editor.Builders
{
    public readonly struct PlayerFoundationTemplateBuildResult
    {
        public PlayerFoundationTemplateBuildResult(string inputActionsPath, string playerPrefabPath)
        {
            InputActionsPath = inputActionsPath;
            PlayerPrefabPath = playerPrefabPath;
        }

        public string InputActionsPath { get; }
        public string PlayerPrefabPath { get; }
    }
}
