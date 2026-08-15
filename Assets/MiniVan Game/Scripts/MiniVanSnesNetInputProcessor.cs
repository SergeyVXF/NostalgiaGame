using System;
using SK.Libretro;
using SK.Libretro.Header;
using SK.Libretro.Unity;

namespace MiniVanGame
{
    /// <summary>
    /// Server-side Libretro input fed by networked joypad bitmasks (ports 0-3).
    /// </summary>
    public sealed class MiniVanSnesNetInputProcessor : IInputProcessor
    {
        public static readonly MiniVanSnesNetInputProcessor Instance = new MiniVanSnesNetInputProcessor();

        private readonly short[] portMasks = new short[4];

        public LeftStickBehaviour LeftStickBehaviour { get; set; }

        public static void RegisterWithLibretro()
        {
            LibretroInputProcessorOverride.Instance = Instance;
        }

        public void SetJoypadMask(int port, ushort mask)
        {
            if (port < 0 || port >= portMasks.Length)
            {
                return;
            }

            portMasks[port] = unchecked((short)mask);
        }

        public void ClearPort(int port) => SetJoypadMask(port, 0);

        public void ClearAll()
        {
            Array.Clear(portMasks, 0, portMasks.Length);
        }

        public short JoypadButton(int port, RETRO_DEVICE_ID_JOYPAD button)
        {
            if (port < 0 || port >= portMasks.Length)
            {
                return 0;
            }

            int bit = (int)button;
            if (bit < 0 || bit > 15)
            {
                return 0;
            }

            return (portMasks[port] & (1 << bit)) != 0 ? (short)1 : (short)0;
        }

        public short JoypadButtons(int port)
        {
            if (port < 0 || port >= portMasks.Length)
            {
                return 0;
            }

            return portMasks[port];
        }

        public short MouseX(int port) => 0;
        public short MouseY(int port) => 0;
        public short MouseWheel(int port) => 0;
        public short MouseButton(int port, RETRO_DEVICE_ID_MOUSE button) => 0;
        public short KeyboardKey(int port, retro_key key) => 0;
        public short LightgunX(int port) => 0;
        public short LightgunY(int port) => 0;
        public bool LightgunIsOffscreen(int port) => true;
        public short LightgunButton(int port, RETRO_DEVICE_ID_LIGHTGUN button) => 0;
        public short AnalogLeftX(int port) => 0;
        public short AnalogLeftY(int port) => 0;
        public short AnalogRightX(int port) => 0;
        public short AnalogRightY(int port) => 0;
        public short PointerX(int port) => 0;
        public short PointerY(int port) => 0;
        public short PointerPressed(int port) => 0;
        public short PointerCount(int port) => 0;
        public bool SetRumbleState(int port, retro_rumble_effect effect, ushort strength) => true;
    }
}
