namespace MiniVanGame
{
    public enum MiniVanGear
    {
        Park = 0,
        Reverse = 1,
        Neutral = 2,
        First = 3,
        Second = 4,
        Third = 5,
        Fourth = 6,
        Fifth = 7
    }

    public static class MiniVanGearUtility
    {
        public static string ToLabel(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.Park:
                    return "P";
                case MiniVanGear.Reverse:
                    return "R";
                case MiniVanGear.Neutral:
                    return "N";
                case MiniVanGear.First:
                    return "1";
                case MiniVanGear.Second:
                    return "2";
                case MiniVanGear.Third:
                    return "3";
                case MiniVanGear.Fourth:
                    return "4";
                case MiniVanGear.Fifth:
                    return "5";
                default:
                    return "?";
            }
        }

        public static bool IsForward(MiniVanGear gear)
        {
            return gear == MiniVanGear.First ||
                   gear == MiniVanGear.Second ||
                   gear == MiniVanGear.Third ||
                   gear == MiniVanGear.Fourth ||
                   gear == MiniVanGear.Fifth;
        }

        public static float MaxForwardSpeed(MiniVanGear gear)
        {
            switch (gear)
            {
                case MiniVanGear.First:
                    return 40f / 3.6f;
                case MiniVanGear.Second:
                    return 65f / 3.6f;
                case MiniVanGear.Third:
                    return 95f / 3.6f;
                case MiniVanGear.Fourth:
                    return 130f / 3.6f;
                case MiniVanGear.Fifth:
                    return 180f / 3.6f;
                default:
                    return 0f;
            }
        }
    }
}
