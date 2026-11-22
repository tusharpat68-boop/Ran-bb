namespace CyberRunner.Obstacles
{
    /// <summary>
    /// Phase 2 obstacle categories with distinct silhouettes and clear counterplay:
    /// - LowRoadBarrier: Jump over (or lane dodge)
    /// - LaserGate: Slide under (or lane dodge)
    /// - TallCyberBlock: Lane dodge
    /// - MovingCyberTrain: Approaching futuristic maglev train (lane dodge)
    /// - HoverSecurityDrone: Mid-height oscillating security drone (slide under or lane dodge)
    /// </summary>
    public enum ObstacleType
    {
        LowRoadBarrier = 0,
        LaserGate = 1,
        TallCyberBlock = 2,
        MovingCyberTrain = 3,
        HoverSecurityDrone = 4
    }
}
