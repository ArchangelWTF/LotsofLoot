using SPTarkov.Server.Core.Models.Eft.Common;

namespace LotsofLoot
{
    public static class ExtensionMethods
    {
        public static bool IsMarkedRoomSpawnpoint(this Spawnpoint spawnpoint, string locationId)
        {
            var pos = spawnpoint.Template?.Position;

            if (pos is null)
            {
                return false;
            }

            if (locationId == "bigmap")
            {
                // Dorms 314 Marked Room
                if (pos.X > 180 && pos.X < 185 && pos.Z > 180 && pos.Z < 185 && pos.Y > 6 && pos.Y < 7)
                {
                    return true;
                }
            }
            else if (locationId == "rezervbase")
            {
                if (pos.X > -125 && pos.X < -120 && pos.Z > 25 && pos.Z < 30 && pos.Y > -15 && pos.Y < -14)
                {
                    return true;
                }
                else if (pos.X > -155 && pos.X < -150 && pos.Z > 70 && pos.Z < 75 && pos.Y > -9 && pos.Y < -8)
                {
                    return true;
                }
                else if (pos.X > 190 && pos.X < 195 && pos.Z > -230 && pos.Z < -225 && pos.Y > -6 && pos.Y < -5)
                {
                    return true;
                }
            }
            else if (locationId == "tarkovstreets")
            {
                // Abandoned Factory Marked Room
                if (pos.X > -133 && pos.X < -129 && pos.Z > 265 && pos.Z < 275 && pos.Y > 8.5 && pos.Y < 11)
                {
                    return true;
                }
                // Chek 13 Marked Room
                else if (pos.X > 186 && pos.X < 191 && pos.Z > 224 && pos.Z < 229 && pos.Y > -0.5 && pos.Y < 1.5)
                {
                    return true;
                }
            }
            else if (locationId == "lighthouse")
            {
                // Lightkeeper marked room
                if (pos.X > 319 && pos.X < 330 && pos.Z > 482 && pos.Z < 489 && pos.Y > 5 && pos.Y < 6.5)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
