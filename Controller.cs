using System;
using System.Threading;

public enum ControllerButton
{
    A = 0,
    B = 1,
    Select = 2,
    Start = 3,
    Up = 4,
    Down = 5,
    Left = 6,
    Right = 7
}

public partial class Emulator
{
    private readonly int[] controllerButtons = new int[2];
    private readonly byte[] controllerShift = new byte[2];
    private bool controllerStrobe;

    public void SetControllerButton(int controller, ControllerButton button, bool pressed)
    {
        if ((uint)controller >= 2) return;
        int bit = 1 << (int)button;
        if (pressed) Interlocked.Or(ref controllerButtons[controller], bit);
        else Interlocked.And(ref controllerButtons[controller], ~bit);
    }

    private void WriteControllerStrobe(byte value)
    {
        bool nextStrobe = (value & 1) != 0;
        if (nextStrobe || controllerStrobe)
            LatchControllers();
        controllerStrobe = nextStrobe;
    }

    private void LatchControllers()
    {
        for (int i = 0; i < controllerShift.Length; i++)
            controllerShift[i] = (byte)Volatile.Read(ref controllerButtons[i]);
    }

    private byte ReadController(int controller, byte openBus)
    {
        if (controllerStrobe)
            return (byte)((openBus & 0xE0) | 0x40 | (Volatile.Read(ref controllerButtons[controller]) & 1));

        byte result = (byte)((openBus & 0xE0) | 0x40 | (controllerShift[controller] & 1));
        controllerShift[controller] = (byte)((controllerShift[controller] >> 1) | 0x80);
        return result;
    }

    private void ResetControllers()
    {
        Array.Clear(controllerButtons, 0, controllerButtons.Length);
        Array.Clear(controllerShift, 0, controllerShift.Length);
        controllerStrobe = false;
    }
}
