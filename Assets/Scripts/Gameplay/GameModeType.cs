namespace CyberRunner.Gameplay
{
    /// <summary>
    /// Modular CYBER RUNNER game modes (Section 11).
    /// </summary>
    public enum GameModeType
    {
        Endless = 0,
        Mission = 1,
        TimeChallenge = 2,
        SpecialChallenge = 3
    }

    /// <summary>
    /// High-level runtime state of an active gameplay session.
    /// </summary>
    public enum RunState
    {
        Running = 0,
        Paused = 1,
        GameOver = 2,
        MainMenu = 3
    }
}
