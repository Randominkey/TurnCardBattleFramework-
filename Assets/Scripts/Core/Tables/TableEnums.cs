namespace Core.Table
{
    public enum Language
    {
        KrString,
        EnString,
        JpString,
    }

    public enum CharacterKeyName
    {
        None = -1,
        Marine,
        Iroha,
        Fubuki,
        Lamy,
        Miko,
        Kanade,
        Pekora,
        Suisei,
        Unknown,
    }

    public enum CharacterFaceName
    {
        Default,
        Angry,
        Sad,
        Smile,
        Surprise,
        Shy,
    }

    public enum BattleCharacterHPState
    {
        FullCondition,
        HalfCondition,
        AboutToDie,
    }

    public enum SkillElement
    {
        None = -1,
        Rock,
        Scissors,
        Paper,
        Require,
        Buff
    }

    public enum UpgradeType
    {
        None,
        Damage,
        Reduce,
        Gauge
    }

    public enum SpeakAction
    {
        None = -1,
        MoveCharacter,
        ShakeCharacter,
        ShakeBg,
        ShakeText,
        CharacterFadeIn,
        CharacterFadeOut,
        BgFadeIn,
        SceneFadeIn,
        SceneFadeOut,
        TextScale,
        ShowImage,
        DeleteImage,
        ShowEffect,
        Delay,
        TextSpeed,
    }

    public enum ModeType
    {
        None,
        Story,
        Reverse
    }

    public enum SceneType
    {
        Title,
        Speak,
        Battle
    }
}
