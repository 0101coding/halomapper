using System.Collections.Generic;

namespace HaloMapper
{
    /// <summary>
    /// Provides context for mapping operations, including recursion tracking.
    /// </summary>
    public class MappingContext
    {
        private readonly HashSet<string> _activeMapppings = new();
        private readonly Dictionary<string, int> _recursionDepth = new();
        private const int MaxRecursionDepth = 10;

        /// <summary>
        /// Checks if a mapping is currently in progress (to detect recursion).
        /// </summary>
        /// <param name="mappingKey">The mapping key (sourceType->destinationType).</param>
        /// <returns>True if recursion is detected.</returns>
        public bool IsInRecursion(string mappingKey)
        {
            if (_recursionDepth.TryGetValue(mappingKey, out var depth))
            {
                return depth >= MaxRecursionDepth;
            }
            return false;
        }

        /// <summary>
        /// Enters a mapping operation.
        /// </summary>
        /// <param name="mappingKey">The mapping key.</param>
        public void EnterMapping(string mappingKey)
        {
            _activeMapppings.Add(mappingKey);
            _recursionDepth[mappingKey] = _recursionDepth.GetValueOrDefault(mappingKey, 0) + 1;
        }

        /// <summary>
        /// Exits a mapping operation.
        /// </summary>
        /// <param name="mappingKey">The mapping key.</param>
        public void ExitMapping(string mappingKey)
        {
            _activeMapppings.Remove(mappingKey);
            if (_recursionDepth.TryGetValue(mappingKey, out var depth))
            {
                if (depth <= 1)
                {
                    _recursionDepth.Remove(mappingKey);
                }
                else
                {
                    _recursionDepth[mappingKey] = depth - 1;
                }
            }
        }

        /// <summary>
        /// Resets the mapping context.
        /// </summary>
        public void Reset()
        {
            _activeMapppings.Clear();
            _recursionDepth.Clear();
        }
    }
}