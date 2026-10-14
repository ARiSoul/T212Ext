using System.Text;

namespace Arisoul.T212Ext.Web.Auth;

/// <summary>
/// Command-line helpers to set up users without the app running:
/// <c>hash-password</c> prints the hash for <c>Auth:Users:N:PasswordHash</c>, and
/// <c>new-totp-secret [username]</c> prints a secret for <c>Auth:Users:N:TotpSecret</c>.
/// Neither touches the database or the network.
/// </summary>
public static class AuthCommands
{
    public static bool IsCommand(string[] args) => args is ["hash-password", ..] or ["new-totp-secret", ..];

    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        switch (args)
        {
            case ["hash-password", ..]:
                var password = ReadSecret("Password: ", input, error);
                if (password.Length < 12)
                {
                    error.WriteLine("Use at least 12 characters.");
                    return 1;
                }
                if (ReadSecret("Again: ", input, error) != password)
                {
                    error.WriteLine("The two passwords differ.");
                    return 1;
                }
                output.WriteLine(SignInService.HashPassword(password));
                return 0;

            case ["new-totp-secret", ..]:
                var secret = Totp.NewSecret();
                var username = args.Length > 1 ? args[1] : "me";
                output.WriteLine(secret);
                error.WriteLine();
                error.WriteLine("Add it to an authenticator app as a setup key (time-based, 6 digits), or open this link on the phone:");
                error.WriteLine(Totp.ProvisioningUri(secret, username));
                return 0;

            default:
                return 1;
        }
    }

    // Typed without echo when there is a console; read as a line when the input is piped.
    private static string ReadSecret(string prompt, TextReader input, TextWriter error)
    {
        error.Write(prompt);
        if (!ReferenceEquals(input, Console.In) || Console.IsInputRedirected)
            return input.ReadLine() ?? "";

        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (text.Length > 0)
                    text.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                text.Append(key.KeyChar);
            }
        }
        error.WriteLine();
        return text.ToString();
    }
}
