using System;

namespace BLL.Simulation
{
    public static class IndustrialRandom
    {
        private static readonly Random Random = new Random();

        public static double Range(double min, double max)
        {
            lock (Random)
            {
                return min + Random.NextDouble() * (max - min);
            }
        }

        public static bool Chance(double probability)
        {
            lock (Random)
            {
                return Random.NextDouble() < probability;
            }
        }
    }
}
