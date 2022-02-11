using Microsoft.AspNetCore.SignalR;
using BGC.Audio;
using TASagentTwitchBot.Plugin.Audio.Midi;
using TASagentTwitchBot.Plugin.XInput;

namespace TASagentStreamBot.KezBoard;

public class ControllerMidiBinding : MidiKeyboardHandler.MidiBinding
{
    private readonly MidiBindingConfig midiBindingConfig;
    private readonly IButtonPressDispatcher buttonPressDispatcher;
    private readonly IHubContext<Web.Hubs.PianoDisplayHub> pianoDisplayHubContext;

    private IKeyPressListener? listener = null;

    public ControllerMidiBinding(
        MidiBindingConfig midiBindingConfig,
        IButtonPressDispatcher buttonPressDispatcher,
        IHubContext<Web.Hubs.PianoDisplayHub> pianoDisplayHubContext)
    {
        this.midiBindingConfig = midiBindingConfig;
        this.buttonPressDispatcher = buttonPressDispatcher;
        this.pianoDisplayHubContext = pianoDisplayHubContext;
    }

    public void SetTemporaryListener(IKeyPressListener listener) => this.listener = listener;
    public void ClearTemporaryListener() => listener = null;

    public override IBGCStream CreateOutputStream(MidiKeyboardHandler midiKeyboardHandler) =>
        new BGC.Audio.Synthesis.SilenceStream(1, 256);

    public override async void HandleNoteOff(int key)
    {
        if (listener is not null)
        {
            listener.HandleKeyUp(key);
        }
        else if (midiBindingConfig.KeyMapping.TryGetValue(key, out DirectXKeyStrokes keyStroke))
        {
            buttonPressDispatcher.TriggerKeyUp(keyStroke);
        }

        await pianoDisplayHubContext.Clients.All.SendAsync("KeyUp", key);
    }

    public override async void HandleNoteOn(int key)
    {
        if (listener is not null)
        {
            listener.HandleKeyDown(key);
        }
        else if (midiBindingConfig.KeyMapping.TryGetValue(key, out DirectXKeyStrokes keyStroke))
        {
            buttonPressDispatcher.TriggerKeyDown(keyStroke);
        }

        await pianoDisplayHubContext.Clients.All.SendAsync("KeyDown", key);
    }

    public override void CleanUp()
    {

    }

    protected override void ChildDisposal()
    {

    }

}
