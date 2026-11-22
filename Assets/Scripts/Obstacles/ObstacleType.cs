namespace CyberRunner.Obstacles
{
    /// <summary>
    /// Cyberpunk obstacle categories across Phases 2 & 3 with distinct silhouettes and clear counterplay:
    /// - LowRoadBarrier: Jump over (or lane dodge)
    /// - LaserGate: Slide under (or lane dodge)
    /// - TallCyberBlock: Lane dodge
    /// - MovingCyberTrain: Approaching futuristic maglev train (lane dodge)
    /// - HoverSecurityDrone: Mid-height oscillating security drone (slide under or lane dodge)
    /// - RotatingIndustrialBar: Industrial Zone spinning hazard beam (jump over or lane dodge)
    /// - ElectricEnergyWall: Digital Core vertical plasma energy wall (lane dodge)
    /// </summary>
    public enum ObstacleType
    {
        LowRoadBarrier = 0,
        LaserGate = 1,
        TallCyberBlock = 2,
        MovingCyberTrain = 3,
        HoverSecurityDrone = 4,
        RotatingIndustrialBar = 5,
        ElectricEnergyWall = 6
    }
}
