using TASagentTwitchBot.Core;
using TASagentTwitchBot.Core.Config;
using TASagentTwitchBot.Plugin.Audio.Midi;
using TASagentTwitchBot.Plugin.XInput;

namespace TASagentStreamBot.KezBoard;

public interface IKeyPressListener
{
    void HandleKeyDown(int key);
    void HandleKeyUp(int key);
}

public class KezBoardView : TASagentTwitchBot.Core.View.BasicView, IKeyPressListener
{
    private readonly ControllerMidiBinding controllerMidiBinding;
    private readonly MidiBindingConfig midiBindingConfig;
    private readonly ICommunication communication;
    private readonly MidiKeyboardHandler midiKeyboardHandler;

    private State state = State.Playing;
    private List<string> midiDevices;
    private int? midiKey = null;

    public KezBoardView(
        BotConfiguration botConfig,
        MidiBindingConfig midiBindingConfig,
        ICommunication communication,
        MidiKeyboardHandler midiKeyboardHandler,
        ControllerMidiBinding controllerMidiBinding,
        ApplicationManagement applicationManagement)
        : base(
            botConfig: botConfig,
            communication: communication,
            applicationManagement: applicationManagement)
    {
        this.controllerMidiBinding = controllerMidiBinding;
        this.midiBindingConfig = midiBindingConfig;
        this.midiKeyboardHandler = midiKeyboardHandler;
        this.communication = communication;

        midiDevices = midiKeyboardHandler.GetMidiDevices();
        bool setDevice = false;

        if (!string.IsNullOrEmpty(midiBindingConfig.MidiDevice) && midiDevices.Contains(midiBindingConfig.MidiDevice))
        {
            //Restore saved device
            setDevice = midiKeyboardHandler.UpdateCurrentMidiDevice(midiBindingConfig.MidiDevice);

            if (setDevice)
            {
                communication.SendDebugMessage($"Midi Device connected from settings: {midiBindingConfig.MidiDevice}");
            }
        }

        if (!setDevice && midiDevices.Count > 0)
        {
            //Set to first device
            setDevice = midiKeyboardHandler.UpdateCurrentMidiDevice(midiDevices[0]);

            if (setDevice)
            {
                communication.SendDebugMessage($"Midi Device set to first device detected: {midiDevices[0]}");
            }
        }

        if (!setDevice)
        {
            communication.SendDebugMessage($"No Midi Device detected");
        }

        PrintInstructions();
    }

    private void PrintInstructions()
    {
        switch (state)
        {
            case State.EditingModeSelection:
                communication.SendDebugMessage(
                    "\n********* EDIT ***********\n" +
                    "Press ESCAPE to end binding editing and enable playing.\n" +
                    "Press A to Add/Edit a binding.\n" +
                    "Press S to Remove a binding.");

                if (midiBindingConfig.KeyMapping.Count > 0)
                {
                    communication.SendDebugMessage($"Current Bindings:\n{string.Join("\n", midiBindingConfig.KeyMapping.Select(x => $"  Key {x.Key} -> {x.Value.ToString()[4..]}"))}");
                }
                else
                {
                    communication.SendDebugMessage($"Current Bindings: NONE");
                }
                break;

            case State.EditingAddBinding:
                communication.SendDebugMessage(
                    "\n********* ADD ***********\n" +
                    "Press the Midi key to bind, then the keyboard key to bind it to.");
                break;

            case State.EditingRemoveBinding:
                communication.SendDebugMessage(
                    "\n********* REMOVE ***********\n" +
                    "Press the Midi key to unbind.");
                break;

            case State.Playing:
                communication.SendDebugMessage(
                    "\n********* PLAY ***********\n" +
                    "Press CTRL-Q to save and quit\n" +
                    "Press A to Change the current Midi Device.\n" +
                    "Press S to Edit current bindings.");
                break;

            case State.SettingDevice:
                communication.SendDebugMessage(
                    "\n********* SET DEVICE ***********\n" +
                    "Press ESCAPE to Abort device selection.\n" +
                    "Press the NUMBER corresponding to a midi device below to select that device.");

                midiDevices = midiKeyboardHandler.GetMidiDevices();
                if (midiDevices.Count > 0)
                {
                    communication.SendDebugMessage($"Midi Devices:\n{string.Join("\n", midiDevices.Select((x,i) => $"  {i+1}) {x}"))}");
                }
                else
                {
                    communication.SendDebugMessage("Midi Devices: None Detected");
                }
                break;

            default:
                break;
        }
    }

    private enum State
    {
        EditingModeSelection = 0,
        EditingAddBinding,
        EditingRemoveBinding,
        Playing,
        SettingDevice
    }

    protected override void SendPublicChatHandler(string message) { }
    protected override void SendWhisperHandler(string username, string message) { }
    protected override void ReceiveMessageHandler(TASagentTwitchBot.Core.IRC.TwitchChatter chatter) { }

    protected override void HandleKeys(in ConsoleKeyInfo input)
    {
        switch (state)
        {
            case State.EditingModeSelection:
                HandleEditingModeSelectionKeys(input);
                break;

            case State.EditingAddBinding:
                HandleEditingAddBindingKeys(input);
                break;

            case State.EditingRemoveBinding:
                HandleEditingRemoveBindingKeys(input);
                break;

            case State.Playing:
                HandlePlayingKeys(input);
                break;

            case State.SettingDevice:
                HandleSettingDeviceKeys(input);
                break;

            default:
                break;
        }
    }

    private void HandleEditingModeSelectionKeys(in ConsoleKeyInfo input)
    {
        switch (input.Key)
        {
            case ConsoleKey.Escape:
                //Leave mode
                controllerMidiBinding.ClearTemporaryListener();
                state = State.Playing;
                communication.SendDebugMessage("Switched to PLAYING mode.\n");
                PrintInstructions();
                break;

            case ConsoleKey.A:
                //Add a binding
                state = State.EditingAddBinding;
                midiKey = null;
                PrintInstructions();
                break;

            case ConsoleKey.S:
                //Remove a binding
                state = State.EditingRemoveBinding;
                midiKey = null;
                PrintInstructions();
                break;
        }
    }

    private void HandleEditingAddBindingKeys(in ConsoleKeyInfo input)
    {
        if (!midiKey.HasValue)
        {
            if (input.Key == ConsoleKey.Escape)
            {
                //Leave mode
                communication.SendDebugMessage("Aborting AddBinding.");
                state = State.EditingModeSelection;
                PrintInstructions();
                return;
            }

            communication.SendDebugMessage("Press a Midi Key first, or ESCAPE to abort.");
            return;
        }

        DirectXKeyStrokes keyStroke = TranslateKey(input);

        if (keyStroke == 0)
        {
            communication.SendDebugMessage($"Key {input.Key} is not supported. Press another key.");
            return;
        }

        midiBindingConfig.KeyMapping[midiKey.Value] = keyStroke;
        communication.SendDebugMessage($"Bound {input.Key} to Midi Key {midiKey.Value}.");
        midiBindingConfig.Serialize();
        midiKey = null;

        state = State.EditingModeSelection;
        PrintInstructions();
    }

    private static DirectXKeyStrokes TranslateKey(in ConsoleKeyInfo input)
    {
        switch (input.Key)
        {
            case ConsoleKey.Backspace: return DirectXKeyStrokes.DIK_BACKSPACE;
            case ConsoleKey.Tab: return DirectXKeyStrokes.DIK_TAB;
            case ConsoleKey.Enter: return DirectXKeyStrokes.DIK_RETURN;
            case ConsoleKey.Escape: return DirectXKeyStrokes.DIK_ESCAPE;
            case ConsoleKey.Spacebar: return DirectXKeyStrokes.DIK_SPACE;
            case ConsoleKey.PageUp: return DirectXKeyStrokes.DIK_PGUP;
            case ConsoleKey.PageDown: return DirectXKeyStrokes.DIK_PGDN;
            case ConsoleKey.End: return DirectXKeyStrokes.DIK_END;
            case ConsoleKey.Home: return DirectXKeyStrokes.DIK_HOME;
            case ConsoleKey.LeftArrow: return DirectXKeyStrokes.DIK_LEFTARROW;
            case ConsoleKey.UpArrow: return DirectXKeyStrokes.DIK_UPARROW;
            case ConsoleKey.RightArrow: return DirectXKeyStrokes.DIK_RIGHTARROW;
            case ConsoleKey.DownArrow: return DirectXKeyStrokes.DIK_DOWNARROW;
            case ConsoleKey.Insert: return DirectXKeyStrokes.DIK_INSERT;
            case ConsoleKey.Delete: return DirectXKeyStrokes.DIK_DELETE;
            case ConsoleKey.D0: return DirectXKeyStrokes.DIK_0;
            case ConsoleKey.D1: return DirectXKeyStrokes.DIK_1;
            case ConsoleKey.D2: return DirectXKeyStrokes.DIK_2;
            case ConsoleKey.D3: return DirectXKeyStrokes.DIK_3;
            case ConsoleKey.D4: return DirectXKeyStrokes.DIK_4;
            case ConsoleKey.D5: return DirectXKeyStrokes.DIK_5;
            case ConsoleKey.D6: return DirectXKeyStrokes.DIK_6;
            case ConsoleKey.D7: return DirectXKeyStrokes.DIK_7;
            case ConsoleKey.D8: return DirectXKeyStrokes.DIK_8;
            case ConsoleKey.D9: return DirectXKeyStrokes.DIK_9;
            case ConsoleKey.A: return DirectXKeyStrokes.DIK_A;
            case ConsoleKey.B: return DirectXKeyStrokes.DIK_B;
            case ConsoleKey.C: return DirectXKeyStrokes.DIK_C;
            case ConsoleKey.D: return DirectXKeyStrokes.DIK_D;
            case ConsoleKey.E: return DirectXKeyStrokes.DIK_E;
            case ConsoleKey.F: return DirectXKeyStrokes.DIK_F;
            case ConsoleKey.G: return DirectXKeyStrokes.DIK_G;
            case ConsoleKey.H: return DirectXKeyStrokes.DIK_H;
            case ConsoleKey.I: return DirectXKeyStrokes.DIK_I;
            case ConsoleKey.J: return DirectXKeyStrokes.DIK_J;
            case ConsoleKey.K: return DirectXKeyStrokes.DIK_K;
            case ConsoleKey.L: return DirectXKeyStrokes.DIK_L;
            case ConsoleKey.M: return DirectXKeyStrokes.DIK_M;
            case ConsoleKey.N: return DirectXKeyStrokes.DIK_N;
            case ConsoleKey.O: return DirectXKeyStrokes.DIK_O;
            case ConsoleKey.P: return DirectXKeyStrokes.DIK_P;
            case ConsoleKey.Q: return DirectXKeyStrokes.DIK_Q;
            case ConsoleKey.R: return DirectXKeyStrokes.DIK_R;
            case ConsoleKey.S: return DirectXKeyStrokes.DIK_S;
            case ConsoleKey.T: return DirectXKeyStrokes.DIK_T;
            case ConsoleKey.U: return DirectXKeyStrokes.DIK_U;
            case ConsoleKey.V: return DirectXKeyStrokes.DIK_V;
            case ConsoleKey.W: return DirectXKeyStrokes.DIK_W;
            case ConsoleKey.X: return DirectXKeyStrokes.DIK_X;
            case ConsoleKey.Y: return DirectXKeyStrokes.DIK_Y;
            case ConsoleKey.Z: return DirectXKeyStrokes.DIK_Z;
            case ConsoleKey.NumPad0: return DirectXKeyStrokes.DIK_NUMPAD0;
            case ConsoleKey.NumPad1: return DirectXKeyStrokes.DIK_NUMPAD1;
            case ConsoleKey.NumPad2: return DirectXKeyStrokes.DIK_NUMPAD2;
            case ConsoleKey.NumPad3: return DirectXKeyStrokes.DIK_NUMPAD3;
            case ConsoleKey.NumPad4: return DirectXKeyStrokes.DIK_NUMPAD4;
            case ConsoleKey.NumPad5: return DirectXKeyStrokes.DIK_NUMPAD5;
            case ConsoleKey.NumPad6: return DirectXKeyStrokes.DIK_NUMPAD6;
            case ConsoleKey.NumPad7: return DirectXKeyStrokes.DIK_NUMPAD7;
            case ConsoleKey.NumPad8: return DirectXKeyStrokes.DIK_NUMPAD8;
            case ConsoleKey.NumPad9: return DirectXKeyStrokes.DIK_NUMPAD9;
            case ConsoleKey.Multiply: return DirectXKeyStrokes.DIK_MULTIPLY;
            case ConsoleKey.Add: return DirectXKeyStrokes.DIK_ADD;
            case ConsoleKey.Subtract: return DirectXKeyStrokes.DIK_MINUS;
            case ConsoleKey.Decimal: return DirectXKeyStrokes.DIK_PERIOD;
            case ConsoleKey.Divide: return DirectXKeyStrokes.DIK_SLASH;
            case ConsoleKey.F1: return DirectXKeyStrokes.DIK_F1;
            case ConsoleKey.F2: return DirectXKeyStrokes.DIK_F2;
            case ConsoleKey.F3: return DirectXKeyStrokes.DIK_F3;
            case ConsoleKey.F4: return DirectXKeyStrokes.DIK_F4;
            case ConsoleKey.F5: return DirectXKeyStrokes.DIK_F5;
            case ConsoleKey.F6: return DirectXKeyStrokes.DIK_F6;
            case ConsoleKey.F7: return DirectXKeyStrokes.DIK_F7;
            case ConsoleKey.F8: return DirectXKeyStrokes.DIK_F8;
            case ConsoleKey.F9: return DirectXKeyStrokes.DIK_F9;
            case ConsoleKey.F10: return DirectXKeyStrokes.DIK_F10;
            case ConsoleKey.F11: return DirectXKeyStrokes.DIK_F11;
            case ConsoleKey.F12: return DirectXKeyStrokes.DIK_F12;
            case ConsoleKey.F13: return DirectXKeyStrokes.DIK_F13;
            case ConsoleKey.F14: return DirectXKeyStrokes.DIK_F14;
            case ConsoleKey.F15: return DirectXKeyStrokes.DIK_F15;
            case ConsoleKey.OemPlus: return DirectXKeyStrokes.DIK_ADD;
            case ConsoleKey.OemComma: return DirectXKeyStrokes.DIK_COMMA;
            case ConsoleKey.OemMinus: return DirectXKeyStrokes.DIK_MINUS;
            case ConsoleKey.OemPeriod: return DirectXKeyStrokes.DIK_PERIOD;
            case ConsoleKey.LeftWindows: return DirectXKeyStrokes.DIK_LWIN;
            case ConsoleKey.RightWindows: return DirectXKeyStrokes.DIK_RWIN;
            case ConsoleKey.Applications: return DirectXKeyStrokes.DIK_APPS;

            case ConsoleKey.Select:
            case ConsoleKey.Print:
            case ConsoleKey.Execute:
            case ConsoleKey.PrintScreen:
            case ConsoleKey.Help:
            case ConsoleKey.Sleep:
            case ConsoleKey.Separator:
            case ConsoleKey.F16:
            case ConsoleKey.F17:
            case ConsoleKey.F18:
            case ConsoleKey.F19:
            case ConsoleKey.F20:
            case ConsoleKey.F21:
            case ConsoleKey.F22:
            case ConsoleKey.F23:
            case ConsoleKey.F24:
            case ConsoleKey.BrowserBack:
            case ConsoleKey.BrowserForward:
            case ConsoleKey.BrowserRefresh:
            case ConsoleKey.BrowserStop:
            case ConsoleKey.BrowserSearch:
            case ConsoleKey.BrowserFavorites:
            case ConsoleKey.BrowserHome:
            case ConsoleKey.VolumeMute:
            case ConsoleKey.VolumeDown:
            case ConsoleKey.VolumeUp:
            case ConsoleKey.MediaNext:
            case ConsoleKey.MediaPrevious:
            case ConsoleKey.MediaStop:
            case ConsoleKey.MediaPlay:
            case ConsoleKey.LaunchMail:
            case ConsoleKey.LaunchMediaSelect:
            case ConsoleKey.LaunchApp1:
            case ConsoleKey.LaunchApp2:
            case ConsoleKey.Oem1:
            case ConsoleKey.Oem2:
            case ConsoleKey.Oem3:
            case ConsoleKey.Oem4:
            case ConsoleKey.Oem5:
            case ConsoleKey.Oem6:
            case ConsoleKey.Oem7:
            case ConsoleKey.Oem8:
            case ConsoleKey.Oem102:
            case ConsoleKey.Process:
            case ConsoleKey.Packet:
            case ConsoleKey.Attention:
            case ConsoleKey.CrSel:
            case ConsoleKey.ExSel:
            case ConsoleKey.EraseEndOfFile:
            case ConsoleKey.Play:
            case ConsoleKey.Zoom:
            case ConsoleKey.NoName:
            case ConsoleKey.Pa1:
            case ConsoleKey.OemClear:
            case ConsoleKey.Pause: 
            case ConsoleKey.Clear:
            default:
                return 0;
        }
    }

    private void HandleEditingRemoveBindingKeys(in ConsoleKeyInfo input)
    {
        switch (input.Key)
        {
            case ConsoleKey.Escape:
                //Leave mode
                communication.SendDebugMessage("Aborting RemoveBinding.");
                state = State.EditingModeSelection;
                PrintInstructions();
                break;
        }
    }

    private void HandleSettingDeviceKeys(in ConsoleKeyInfo input)
    {
        switch (input.Key)
        {
            case ConsoleKey.Escape:
                //Leave mode
                state = State.Playing;
                communication.SendDebugMessage("Switched to PLAYING mode.\n");
                PrintInstructions();
                break;

            case ConsoleKey.D1:
            case ConsoleKey.D2:
            case ConsoleKey.D3:
            case ConsoleKey.D4:
            case ConsoleKey.D5:
            case ConsoleKey.D6:
            case ConsoleKey.D7:
            case ConsoleKey.D8:
            case ConsoleKey.D9:
                {
                    int value = input.Key - ConsoleKey.D1;
                    if (value < midiDevices.Count)
                    {
                        midiBindingConfig.MidiDevice = midiDevices[value];
                        midiKeyboardHandler.UpdateCurrentMidiDevice(midiBindingConfig.MidiDevice);

                        midiBindingConfig.Serialize();

                        goto case ConsoleKey.Escape;
                    }
                }
                break;
        }
    }

    private void HandlePlayingKeys(in ConsoleKeyInfo input)
    {
        switch (input.Key)
        {
            case ConsoleKey.A:
                //Switch to Device Selection
                state = State.SettingDevice;
                communication.SendDebugMessage("Switched to DEVICE SELECTION mode.\n");
                PrintInstructions();
                break;

            case ConsoleKey.S:
                //Switch to Editor
                controllerMidiBinding.SetTemporaryListener(this);
                state = State.EditingModeSelection;
                communication.SendDebugMessage("Switched to EDITING mode.\n");
                PrintInstructions();
                break;
        }
    }

    public void HandleKeyDown(int key)
    {
        switch (state)
        {
            case State.EditingAddBinding:
                if (midiKey.HasValue)
                {
                    if (midiKey.Value != key)
                    {
                        communication.SendDebugMessage($"Key {key} received but Key {midiKey.Value} has already been selected.");
                    }
                }
                else
                {
                    midiKey = key;
                    communication.SendDebugMessage($"Key {key} selected.");
                }
                break;

            case State.EditingRemoveBinding:
                if (midiBindingConfig.KeyMapping.Remove(key))
                {
                    communication.SendDebugMessage($"Key {key} unbound.");
                    state = State.EditingModeSelection;
                    PrintInstructions();
                }
                break;
        }
    }

    //Don't care
    public void HandleKeyUp(int key) { }
}
