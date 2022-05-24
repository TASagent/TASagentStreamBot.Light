namespace TASagentTwitchBot.Light;

public class AudioConfigurator : Core.BaseConfigurator
{
    public AudioConfigurator(
        Core.Config.BotConfiguration botConfig,
        Core.ICommunication communication,
        Core.ErrorHandler errorHandler)
        : base(botConfig, communication, errorHandler)
    {
    }

    public override Task<bool> VerifyConfigured()
    {
        bool successful = true;

        successful |= ConfigureAdminPassword();

        successful |= ConfigureAudioOutputDevices();
        successful |= ConfigureAudioInputDevices();

        return Task.FromResult(successful);
    }

    protected bool ConfigureAdminPassword()
    {
        bool successful = true;
        if (string.IsNullOrEmpty(botConfig.AuthConfiguration.Admin.PasswordHash))
        {
            WritePrompt("Admin password for bot control");

            string pass = Console.ReadLine()!.Trim();

            if (!string.IsNullOrEmpty(pass))
            {
                botConfig.AuthConfiguration.Admin.PasswordHash = Core.Cryptography.HashPassword(pass);
                botConfig.Serialize();
            }
            else
            {
                WriteError("Empty Admin Password received.");
                successful = false;
            }

            botConfig.AuthConfiguration.Privileged.PasswordHash = Core.Cryptography.HashPassword(Guid.NewGuid().ToString());
            botConfig.AuthConfiguration.User.PasswordHash = Core.Cryptography.HashPassword(Guid.NewGuid().ToString());
        }

        return successful;
    }
}
