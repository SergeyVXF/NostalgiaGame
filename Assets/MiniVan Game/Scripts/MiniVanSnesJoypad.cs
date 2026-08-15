using UnityEngine;

namespace MiniVanGame
{
    /// <summary>
    /// Samples the MiniVan SNES keyboard layout into a Libretro joypad bitmask
    /// (bit index = RETRO_DEVICE_ID_JOYPAD).
    /// </summary>
    public static class MiniVanSnesJoypad
    {
        // Matches DrawSnesControlsHint / LibretroInputActions remap.
        public static ushort SampleLocalBitmask()
        {
            ushort mask = 0;
            if (Input.GetKey(KeyCode.J))
            {
                mask |= 1 << 0; // B
            }

            if (Input.GetKey(KeyCode.L))
            {
                mask |= 1 << 1; // Y
            }

            if (Input.GetKey(KeyCode.X))
            {
                mask |= 1 << 2; // Select
            }

            if (Input.GetKey(KeyCode.Z))
            {
                mask |= 1 << 3; // Start
            }

            if (Input.GetKey(KeyCode.W))
            {
                mask |= 1 << 4; // Up
            }

            if (Input.GetKey(KeyCode.S))
            {
                mask |= 1 << 5; // Down
            }

            if (Input.GetKey(KeyCode.A))
            {
                mask |= 1 << 6; // Left
            }

            if (Input.GetKey(KeyCode.D))
            {
                mask |= 1 << 7; // Right
            }

            if (Input.GetKey(KeyCode.H))
            {
                mask |= 1 << 8; // A
            }

            if (Input.GetKey(KeyCode.K))
            {
                mask |= 1 << 9; // X
            }

            if (Input.GetKey(KeyCode.U))
            {
                mask |= 1 << 10; // L
            }

            if (Input.GetKey(KeyCode.I))
            {
                mask |= 1 << 11; // R
            }

            return mask;
        }
    }
}
