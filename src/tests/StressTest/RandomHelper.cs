using System;

namespace StressTest
{
    /// <summary>
    /// Set of thread-safe helpers for dealing with random numbers
    /// </summary>
    public static class RandomHelper
    {
        [ThreadStatic]
        private static Random s_random;

        /// <summary>
        /// Gets the current random number generator for this thread
        /// </summary>
        private static Random CurrentRandom
        {
            get
            {
                if (s_random == null)
                {
                    s_random = new Random();
                }
                return s_random;
            }
        }

        /// <summary>
        /// Gets the next bool with the provided weighted probability
        /// </summary>
        /// <param name="probabilityOfSuccess">Percentage probability (from 0-100) of returning true</param>
        public static bool NextBoolWithProbability(int probabilityOfSuccess)
        {
            return CurrentRandom.Next(100) <= probabilityOfSuccess;
        }

        /// <summary>
        /// Randomly selects an item from the given array
        /// </summary>
        public static T SelectFromArray<T>(T[] items)
        {
            return items[CurrentRandom.Next(items.Length)];
        }

        /// <summary>
        /// Returns a random number between min (inclusive) and max (exclusive)
        /// </summary>
        public static int Next(int min, int max)
        {
            return CurrentRandom.Next(min, max);
        }
    }
}
