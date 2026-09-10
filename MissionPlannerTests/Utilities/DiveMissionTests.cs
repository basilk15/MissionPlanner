using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MissionPlanner.Utilities;

namespace MissionPlannerTests.Utilities
{
    [TestClass]
    public class DiveMissionTests
    {
        [TestMethod]
        public void FriendlyCommandsMapToExistingMavlinkCommands()
        {
            ushort command;

            Assert.IsTrue(DiveMission.TryGetMavCommand(DiveMission.DiveCommandName, out command));
            Assert.AreEqual((ushort) MAVLink.MAV_CMD.SCRIPT_TIME, command);
            Assert.AreEqual((ushort) 200, DiveMission.DiveScriptCommandId);

            Assert.IsTrue(DiveMission.TryGetMavCommand(DiveMission.TargetPointCommandName, out command));
            Assert.AreEqual((ushort) MAVLink.MAV_CMD.WAYPOINT, command);
        }

        [TestMethod]
        public void DownloadedDivePairGetsFriendlyNames()
        {
            Assert.AreEqual(DiveMission.DiveCommandName,
                DiveMission.GetDisplayCommand((ushort) MAVLink.MAV_CMD.SCRIPT_TIME,
                    DiveMission.DiveScriptCommandId, null, null));
            Assert.AreEqual(DiveMission.TargetPointCommandName,
                DiveMission.GetDisplayCommand((ushort) MAVLink.MAV_CMD.WAYPOINT,
                    0, (ushort) MAVLink.MAV_CMD.SCRIPT_TIME, DiveMission.DiveScriptCommandId));
            Assert.IsNull(DiveMission.GetDisplayCommand((ushort) MAVLink.MAV_CMD.WAYPOINT,
                0, (ushort) MAVLink.MAV_CMD.WAYPOINT, 0));
        }

        [TestMethod]
        public void GenericScriptTimeIsNotDisplayedAsDive()
        {
            Assert.IsNull(DiveMission.GetDisplayCommand((ushort) MAVLink.MAV_CMD.SCRIPT_TIME,
                0, null, null));
            Assert.IsNull(DiveMission.GetDisplayCommand((ushort) MAVLink.MAV_CMD.WAYPOINT,
                0, (ushort) MAVLink.MAV_CMD.SCRIPT_TIME, 32));
        }

        [TestMethod]
        public void MultipleAdjacentDivePairsAreValid()
        {
            var rows = new List<DiveMissionRow>
            {
                Row("WAYPOINT", 1, 1),
                Row(DiveMission.DiveCommandName),
                Row(DiveMission.TargetPointCommandName, 10, 20),
                Row("WAYPOINT", 2, 2),
                Row(DiveMission.DiveCommandName),
                Row(DiveMission.TargetPointCommandName, -10, -20)
            };

            Assert.IsNull(DiveMission.Validate(rows));
        }

        [TestMethod]
        public void DiveWithoutImmediateTargetIsInvalid()
        {
            var error = DiveMission.Validate(new List<DiveMissionRow>
            {
                Row(DiveMission.DiveCommandName),
                Row("WAYPOINT", 10, 20)
            });

            StringAssert.Contains(error, "followed immediately");
        }

        [TestMethod]
        public void OrphanOrUnsetTargetIsInvalid()
        {
            var orphanError = DiveMission.Validate(new List<DiveMissionRow>
            {
                Row(DiveMission.TargetPointCommandName, 10, 20)
            });
            StringAssert.Contains(orphanError, "immediately follow DIVE");

            var unsetError = DiveMission.Validate(new List<DiveMissionRow>
            {
                Row(DiveMission.DiveCommandName),
                Row(DiveMission.TargetPointCommandName, 0, 0)
            });
            StringAssert.Contains(unsetError, "invalid latitude/longitude");
        }

        private static DiveMissionRow Row(string command, double latitude = 0, double longitude = 0)
        {
            return new DiveMissionRow
            {
                Command = command,
                Latitude = latitude,
                Longitude = longitude
            };
        }
    }
}
