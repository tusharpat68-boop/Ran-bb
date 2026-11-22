namespace CyberRunner.Player
{
    /// <summary>
    /// High-level locomotion state of the Cyber Runner character.
    /// Phase 1 exercises Idle, Running, Jumping, Falling, and Landing;
    /// Sliding, Stumbled, and Dead are reserved for Phase 2+.
    /// </summary>
    public enum PlayerState
    {
        Idle = 0,
        Running = 1,
        Jumping = 2,
        Falling = 3,
        Landing = 4,
        Sliding = 5,
        Stumbled = 6,
        Dead = 7
    }
}
