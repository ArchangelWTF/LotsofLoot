using SPTarkov.Server.Core.Models.Eft.Common;

namespace LotsofLoot;

public static class SpawnpointExtensionMethods
{
    public static bool IsMarkedRoomSpawnpoint(this Spawnpoint spawnpoint, string locationId)
    {
        if (spawnpoint.Template?.Position is null)
        {
            return false;
        }

        var pos = spawnpoint.Template.Position.Value;

        if (locationId == "bigmap")
        {
            // Dorms 314 Marked Room
            if (pos.X > 180 && pos.X < 190 && pos.Y > 5 && pos.Y < 8 && pos.Z > 180 && pos.Z < 185)
            {
                return true;
            }
        }
        else if (locationId == "rezervbase")
        {
            // RB-PKPM
            if (pos.X > -125 && pos.X < -120 && pos.Y > -15 && pos.Y < -13 && pos.Z > 25 && pos.Z < 31)
            {
                return true;
            }

            // RB-BK
            if (pos.X > -157 && pos.X < -150 && pos.Y > -10 && pos.Y < -7 && pos.Z > 70 && pos.Z < 76)
            {
                return true;
            }

            // RB-VO
            if (pos.X > 189 && pos.X < 195 && pos.Y > -7 && pos.Y < -5 && pos.Z > -230 && pos.Z < -224)
            {
                return true;
            }
        }
        else if (locationId == "tarkovstreets")
        {
            // Abandoned Factory Marked Room
            if (pos.X > -134 && pos.X < -128 && pos.Z > 264 && pos.Z < 276 && pos.Y > 8.5 && pos.Y < 12)
            {
                return true;
            }
            // Chek 13 Marked Room
            else if (pos.X > 184 && pos.X < 192 && pos.Z > 223 && pos.Z < 230 && pos.Y > -1.0 && pos.Y < 1.5)
            {
                return true;
            }
        }
        else if (locationId == "lighthouse")
        {
            // Lightkeeper marked room
            if (pos.X > 318 && pos.X < 331 && pos.Z > 481 && pos.Z < 490 && pos.Y > 4 && pos.Y < 7)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsRefKeySpawnpoint(this Spawnpoint spawnpoint, string locationId)
    {
        if (spawnpoint.Template?.Position is null)
        {
            return false;
        }

        var pos = spawnpoint.Template.Position.Value;

        if (locationId == "woods")
        {
            //Shatun's Hideout
            if (pos.X > -513 && pos.X < -509 && pos.Z > -396 && pos.Z < -387 && pos.Y > 14 && pos.Y < 19)
            {
                return true;
            }
        }
        else if (locationId == "interchange")
        {
            //Grumpy's hideout
            if (pos.X > -199 && pos.X < -192 && pos.Z > -348 && pos.Z < -343 && pos.Y > 20 && pos.Y < 25)
            {
                return true;
            }
        }
        else if (locationId == "shoreline")
        {
            //Voron's Hideout
            if (pos.X > -238 && pos.X < -236 && pos.Z > 190 && pos.Z < 1200 && pos.Y > -42 && pos.Y < -36)
            {
                return true;
            }
        }
        else if (locationId == "lighthouse")
        {
            //Leon's Hideout
            if (pos.X > -80 && pos.X < -74 && pos.Z > -298 && pos.Z < -288 && pos.Y > 9 && pos.Y < 13)
            {
                return true;
            }
        }

        return false;
    }
}
