namespace TASagentStreamBot.KezBoard;

public class Configurator : TASagentTwitchBot.Core.BaseConfigurator
{
    public Configurator(
        TASagentTwitchBot.Core.Config.BotConfiguration botConfig,
        TASagentTwitchBot.Core.ICommunication communication,
        TASagentTwitchBot.Core.ErrorHandler errorHandler)
        : base(botConfig, communication, errorHandler)
    {

    }

    public override Task<bool> VerifyConfigured()
    {
        bool changed = false;

        if (string.IsNullOrEmpty(botConfig.AuthConfiguration.Admin.PasswordHash))
        {
            botConfig.AuthConfiguration.Admin.PasswordHash = TASagentTwitchBot.Core.Cryptography.HashPassword("loltesting");
            botConfig.AuthConfiguration.Privileged.PasswordHash = TASagentTwitchBot.Core.Cryptography.HashPassword(Guid.NewGuid().ToString());
            botConfig.AuthConfiguration.User.PasswordHash = TASagentTwitchBot.Core.Cryptography.HashPassword(Guid.NewGuid().ToString());

            changed = true;
        }

        if (string.IsNullOrEmpty(botConfig.EffectOutputDevice) || string.IsNullOrEmpty(botConfig.VoiceOutputDevice))
        {
            List<string> devices = GetAudioOutputDevicesList();
            botConfig.EffectOutputDevice = devices[0];
            botConfig.VoiceOutputDevice = devices[0];

            botConfig.MicConfiguration.Enabled = false;

            changed = true;
        }

        if (string.IsNullOrEmpty(botConfig.VoiceInputDevice))
        {
            List<string> devices = GetAudioInputDevicesList();
            botConfig.VoiceInputDevice = devices[0];

            changed = true;
        }


        if (changed)
        {
            botConfig.Serialize();
        }

        return Task.FromResult(true);
    }
}
