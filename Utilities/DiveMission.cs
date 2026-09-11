using System;
using System.Collections.Generic;

namespace MissionPlanner.Utilities
{
    /// <summary>
    /// Mission Planner presentation and validation for the onboard scripted dive.
    /// No new MAVLink commands are introduced: DIVE is NAV_SCRIPT_TIME and its
    /// immediately following TARGET POINT is a normal NAV_WAYPOINT.
    /// </summary>
    public static class DiveMission
    {
        public const string DiveCommandName = "DIVE";
        public const string TargetPointCommandName = "TARGET POINT";
        public const string DiveModeActionName = "Dive Mode";
        // MAV_CMD_NAV_SCRIPT_TIME.param1 is an application-defined command ID (0..255).
        // 200 is reserved by this custom aircraft integration for the dive script.
        public const ushort DiveScriptCommandId = 200;

        public static readonly ushort DiveMavCommand = (ushort) MAVLink.MAV_CMD.SCRIPT_TIME;
        public static readonly ushort TargetPointMavCommand = (ushort) MAVLink.MAV_CMD.WAYPOINT;

        public static bool TryGetMavCommand(string displayCommand, out ushort mavCommand)
        {
            if (string.Equals(displayCommand, DiveCommandName, StringComparison.Ordinal))
            {
                mavCommand = DiveMavCommand;
                return true;
            }

            if (string.Equals(displayCommand, TargetPointCommandName, StringComparison.Ordinal))
            {
                mavCommand = TargetPointMavCommand;
                return true;
            }

            mavCommand = 0;
            return false;
        }

        public static bool IsDiveMissionItem(ushort mavCommand, float scriptCommandId)
        {
            return mavCommand == DiveMavCommand && scriptCommandId == DiveScriptCommandId;
        }

        public static string GetDisplayCommand(ushort mavCommand, float scriptCommandId,
            ushort? previousMavCommand, float? previousScriptCommandId)
        {
            if (IsDiveMissionItem(mavCommand, scriptCommandId))
                return DiveCommandName;

            if (mavCommand == TargetPointMavCommand &&
                previousMavCommand.HasValue && previousScriptCommandId.HasValue &&
                IsDiveMissionItem(previousMavCommand.Value, previousScriptCommandId.Value))
                return TargetPointCommandName;

            return null;
        }

        public static string Validate(IList<DiveMissionRow> rows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];

                if (IsDiveRow(row))
                {
                    if (index + 1 >= rows.Count ||
                        !IsTargetPointRow(rows, index + 1))
                    {
                        return $"DIVE at mission row {index + 1} must be followed immediately by TARGET POINT.";
                    }
                }

                if (IsTargetPointRow(rows, index))
                {
                    if (index == 0 ||
                        !IsDiveRow(rows[index - 1]))
                    {
                        return $"TARGET POINT at mission row {index + 1} must immediately follow DIVE.";
                    }

                    if (!IsValidTarget(row.Latitude, row.Longitude))
                    {
                        return $"TARGET POINT at mission row {index + 1} has an invalid latitude/longitude.";
                    }
                }
            }

            return null;
        }

        private static bool IsDiveRow(DiveMissionRow row)
        {
            return string.Equals(row.Command, DiveCommandName, StringComparison.Ordinal) ||
                   IsDiveMissionItem(row.MavCommand, row.Param1);
        }

        private static bool IsTargetPointRow(IList<DiveMissionRow> rows, int index)
        {
            var row = rows[index];
            if (string.Equals(row.Command, TargetPointCommandName, StringComparison.Ordinal))
                return true;

            // On the MAVLink wire TARGET POINT is deliberately an ordinary waypoint.
            // Its position immediately after SCRIPT_TIME command ID 200 defines it as
            // the target, even when a UI/runtime displays the raw command names.
            return row.MavCommand == TargetPointMavCommand &&
                   index > 0 && IsDiveRow(rows[index - 1]);
        }

        public static bool IsValidTarget(double latitude, double longitude)
        {
            return !double.IsNaN(latitude) && !double.IsInfinity(latitude) &&
                   !double.IsNaN(longitude) && !double.IsInfinity(longitude) &&
                   latitude >= -90.0 && latitude <= 90.0 &&
                   longitude >= -180.0 && longitude <= 180.0 &&
                   (latitude != 0.0 || longitude != 0.0);
        }
    }

    public sealed class DiveMissionRow
    {
        public string Command { get; set; }
        public ushort MavCommand { get; set; }
        public float Param1 { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
