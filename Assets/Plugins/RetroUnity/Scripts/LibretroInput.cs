using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RetroUnity
{
    public partial class LibretroWrapper
    {
        public partial class Wrapper
        {
            private void RetroInputPoll()
            {
            }

            public static short RetroInputState(uint port, uint device, uint index, uint id)
            {
                switch (device)
                {
                    case 1: //retro device joypad
                        {
                            switch (id)
                            {
                                case 0:
                                    return Input.GetKey(KeyCode.Z) || Input.GetButton("B") ? (short)1 : (short)0; // B
                                case 1:
                                    return Input.GetKey(KeyCode.A) || Input.GetButton("Y") ? (short)1 : (short)0; // Y
                                case 2:
                                    return Input.GetKey(KeyCode.Space) || Input.GetButton("SELECT") ? (short)1 : (short)0; // SELECT
                                case 3:
                                    return Input.GetKey(KeyCode.Return) || Input.GetButton("START") ? (short)1 : (short)0; // START
                                case 4:
                                    return Input.GetKey(KeyCode.UpArrow) || Input.GetAxisRaw("DpadX") >= 1.0f ? (short)1 : (short)0; // UP
                                case 5:
                                    return Input.GetKey(KeyCode.DownArrow) || Input.GetAxisRaw("DpadX") <= -1.0f ? (short)1 : (short)0; // DOWN
                                case 6:
                                    return Input.GetKey(KeyCode.LeftArrow) || Input.GetAxisRaw("DpadY") <= -1.0f ? (short)1 : (short)0; // LEFT
                                case 7:
                                    return Input.GetKey(KeyCode.RightArrow) || Input.GetAxisRaw("DpadY") >= 1.0f ? (short)1 : (short)0; // RIGHT
                                case 8:
                                    return Input.GetKey(KeyCode.X) || Input.GetButton("A") ? (short)1 : (short)0; // A
                                case 9:
                                    return Input.GetKey(KeyCode.S) || Input.GetButton("X") ? (short)1 : (short)0; // X
                                case 10:
                                    return Input.GetKey(KeyCode.Q) || Input.GetButton("L") ? (short)1 : (short)0; // L || L1
                                case 11:
                                    return Input.GetKey(KeyCode.W) || Input.GetButton("R") ? (short)1 : (short)0; // R || R1
                                case 12:
                                    return Input.GetKey(KeyCode.E) ? (short)1 : (short)0; //L2?
                                case 13:
                                    return Input.GetKey(KeyCode.R) ? (short)1 : (short)0; //R2?
                                case 14:
                                    return Input.GetKey(KeyCode.T) ? (short)1 : (short)0; //L3? (Left stick press?)
                                case 15:
                                    return Input.GetKey(KeyCode.Y) ? (short)1 : (short)0; //R3? (Right stick press?)
                                default:
                                    return 0;
                            }
                            break;
                        }

                    case 5: //retro device analog
                            // * axis values in the full analog range of [-0x7fff, 0x7fff], (-32767 to 32767)
                            // *although some devices may return -0x8000.
                            //* Positive X axis is right.Positive Y axis is down.
                            //* Buttons are returned in the range[0, 0x7fff]. (0 to 32767)
                            //#define RETRO_DEVICE_INDEX_ANALOG_LEFT       0
                            //#define RETRO_DEVICE_INDEX_ANALOG_RIGHT      1
                            //#define RETRO_DEVICE_INDEX_ANALOG_BUTTON     2
                            //#define RETRO_DEVICE_ID_ANALOG_X             0
                            //#define RETRO_DEVICE_ID_ANALOG_Y             1
                        switch (index)
                        {
                            case 0: //analog left (stick)
                                switch (id)
                                {
                                    case 0:
                                        return Input.GetKey(KeyCode.Y) ? (short)1 : (short)0; //L analog X
                                    case 1:
                                        return Input.GetKey(KeyCode.Y) ? (short)1 : (short)0; //L analog Y
                                    default: return 0;
                                }
                                break;
                            case 1: //analog right (stick)
                                switch (id)
                                {
                                    case 0:
                                        return Input.GetKey(KeyCode.Y) ? (short)1 : (short)0; //R analog X
                                    case 1:
                                        return Input.GetKey(KeyCode.Y) ? (short)1 : (short)0; //R analog Y
                                    default: return 0;
                                }
                                break;
                            case 2: //analog button?
                                return 0;
                                break;
                            default: return 0;
                        }
                        break;
                    default: return 0;
                }
            }
        }
    }
}