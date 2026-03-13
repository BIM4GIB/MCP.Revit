using Autodesk.Revit.DB;

namespace RevitMcpBridge
{
    /// <summary>
    /// Version-safe helpers for ElementId operations.
    ///
    /// Revit 2024 deprecated ElementId(int) and IntegerValue in favour of
    /// ElementId(long) and Value. Revit 2026 removed the deprecated members
    /// entirely. This helper lets the rest of the codebase use a single call
    /// site regardless of which Revit version is targeted.
    /// </summary>
    public static class ElementIdHelper
    {
        /// <summary>
        /// Create an ElementId from a 64-bit value.
        /// On Revit 2022-2023 the long is narrowed to int (safe for all
        /// real-world element ids in those versions).
        /// </summary>
        public static ElementId Create(long id)
        {
#if REVIT_V_GTE_2024
            // Revit 2024+ has ElementId(long)
            return new ElementId(id);
#else
            // Revit 2022-2023 only has ElementId(int)
            return new ElementId((int)id);
#endif
        }

        /// <summary>
        /// Get the numeric value of an ElementId as a 64-bit long.
        /// Uses .Value on 2026+ (where IntegerValue is removed)
        /// and .IntegerValue on older versions.
        /// </summary>
        public static long GetValue(ElementId elementId)
        {
#if REVIT_V_GTE_2026
            // Revit 2026+: IntegerValue removed, use Value (long)
            return elementId.Value;
#elif REVIT_V_GTE_2024
            // Revit 2024-2025: both exist, prefer Value to avoid deprecation warning
            return elementId.Value;
#else
            // Revit 2022-2023: only IntegerValue (int) exists
            return elementId.IntegerValue;
#endif
        }
    }
}
