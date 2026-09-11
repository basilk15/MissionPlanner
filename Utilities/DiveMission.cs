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
        public const string DiveModeName = MissionPlanner.ArduPilot.Common.DiveModeName;
        public const uint DiveModeCustomMode = (uint) MissionPlanner.ArduPilot.Common.DiveModeCustomMode;
        // MAV_CMD_NAV_SCRIPT_TIME.param1 is an application-defined command ID (0..255).
        // 200 is reserved by this custom aircraft integration for the dive script.
        public const ushort DiveScriptCommandId = 200;

        public static readonly ushort DiveMavCommand = (ushort) MAVLink.MAV_CMD.SCRIPT_TIME;
        public static readonly ushort TargetPointMavCommand = (ushort) MAVLink.MAV_CMD.WAYPOINT;

        public enum ActivationStep
        {
            MissionCurrentRejected,
            MissionCurrentConfirmationTimedOut,
            RequestDiveMode,
            Activated
        }

        public sealed class DiveTargetPair
        {
            public int DiveSequence { get; set; }
            public int TargetSequence { get; set; }
            public double TargetLatitude { get; set; }
            public double TargetLongitude { get; set; }
        }

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

        /// <summary>
        /// Finds the first raw DIVE item at or after minimumSequence and validates its
        /// immediate raw waypoint target.  The sequence adjacency check is kept here
        /// because a mission dictionary can have gaps even when its rows are ordered.
        /// </summary>
        public static bool TryFindNextDiveTargetPair(IList<DiveMissionRow> rows, int minimumSequence,
            out DiveTargetPair pair, out string validationError)
        {
            pair = null;
            validationError = null;

            var diveIndex = -1;
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index].Sequence >= minimumSequence && IsDiveRow(rows[index]))
                {
                    diveIndex = index;
                    break;
                }
            }

            if (diveIndex < 0)
            {
                validationError =
                    "No upcoming DIVE item is present in the aircraft mission. Read or upload a mission containing a valid DIVE -> TARGET POINT pair first.";
                return false;
            }

            var diveRow = rows[diveIndex];
            if (diveIndex + 1 >= rows.Count ||
                rows[diveIndex + 1].Sequence != diveRow.Sequence + 1 ||
                !IsTargetPointRow(rows, diveIndex + 1))
            {
                validationError =
                    $"DIVE mission item {diveRow.Sequence} is not followed immediately by a TARGET POINT waypoint.";
                return false;
            }

            var targetRow = rows[diveIndex + 1];
            validationError = Validate(new List<DiveMissionRow> {diveRow, targetRow});
            if (validationError != null)
                return false;

            pair = new DiveTargetPair
            {
                DiveSequence = diveRow.Sequence,
                TargetSequence = targetRow.Sequence,
                TargetLatitude = targetRow.Latitude,
                TargetLongitude = targetRow.Longitude
            };
            return true;
        }

        public static ActivationStep GetActivationStep(bool missionCurrentAccepted,
            bool missionCurrentConfirmed, bool diveModeHeartbeatObserved)
        {
            if (!missionCurrentAccepted)
                return ActivationStep.MissionCurrentRejected;

            if (!missionCurrentConfirmed)
                return ActivationStep.MissionCurrentConfirmationTimedOut;

            return diveModeHeartbeatObserved ? ActivationStep.Activated : ActivationStep.RequestDiveMode;
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
        public int Sequence { get; set; }
        public string Command { get; set; }
        public ushort MavCommand { get; set; }
        public float Param1 { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
