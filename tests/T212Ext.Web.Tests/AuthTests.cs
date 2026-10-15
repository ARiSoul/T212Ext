using System.Text;
using Arisoul.T212Ext.Web.Auth;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Web.Tests;

public class TotpTests
{
    // RFC 6238, appendix B (SHA1 key), keeping the last 6 of the 8 digits.
    private static readonly byte[] RfcKey = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Codes_match_the_rfc_6238_test_vectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, Totp.Code(RfcKey, Totp.StepOf(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))));

    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32_follows_rfc_4648_without_padding(string text, string expected)
    {
        Assert.Equal(expected, Totp.Base32Encode(Encoding.ASCII.GetBytes(text)));
        Assert.Equal(text, Encoding.ASCII.GetString(Totp.Base32Decode(expected)));
    }

    [Fact]
    public void Base32_decoding_ignores_case_spaces_and_padding()
    {
        Assert.Equal("foobar"u8.ToArray(), Totp.Base32Decode("mzxw 6ytb oi======"));
        Assert.Throws<FormatException>(() => Totp.Base32Decode("MZXW1"));
    }

    [Fact]
    public void A_code_is_accepted_one_step_either_side_and_not_further()
    {
        var secret = Totp.Base32Encode(RfcKey);
        var now = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var step = Totp.StepOf(now);

        Assert.Equal(step, Totp.Match(secret, "005924", now));
        Assert.Equal(step, Totp.Match(secret, "005 924", now.AddSeconds(30)));
        Assert.Equal(step, Totp.Match(secret, "005924", now.AddSeconds(-30)));
        Assert.Null(Totp.Match(secret, "005924", now.AddSeconds(90)));
        Assert.Null(Totp.Match(secret, "00592", now));
        Assert.Null(Totp.Match(secret, "abcdef", now));
        Assert.Null(Totp.Match(secret, null, now));
    }

    [Fact]
    public void New_secrets_are_160_bits()
    {
        var secret = Totp.NewSecret();

        Assert.Equal(32, secret.Length);
        Assert.Equal(20, Totp.Base32Decode(secret).Length);
        Assert.NotEqual(secret, Totp.NewSecret());
    }
}

public class SignInServiceTests
{
    private const string Password = "correct horse battery";
    private static readonly string Hash = SignInService.HashPassword(Password);
    private static readonly string Secret = Totp.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890"));

    // 2009-02-13 23:31:30 UTC, whose code is 005924 (RFC 6238).
    private readonly FixedTime _time = new(DateTimeOffset.FromUnixTimeSeconds(1234567890));

    private SignInService Service(params AuthUser[] users) =>
        new(Options.Create(new AuthOptions { Users = [.. users] }), _time);

    [Fact]
    public void The_right_password_signs_in_whatever_the_case_of_the_username()
    {
        var service = Service(new AuthUser { Username = "Ricardo", PasswordHash = Hash });

        Assert.Equal("Ricardo", service.Check(" ricardo ", Password, null)?.Username);
        Assert.False(service.RequiresCode);
    }

    [Fact]
    public void A_wrong_password_or_an_unknown_user_does_not()
    {
        var service = Service(new AuthUser { Username = "ricardo", PasswordHash = Hash });

        Assert.Null(service.Check("ricardo", "wrong", null));
        Assert.Null(service.Check("ricardo", null, null));
        Assert.Null(service.Check("someone", Password, null));
        Assert.Null(service.Check(null, Password, null));
    }

    [Fact]
    public void With_a_secret_the_code_is_required_and_works_only_once()
    {
        var service = Service(new AuthUser { Username = "ricardo", PasswordHash = Hash, TotpSecret = Secret });

        Assert.True(service.RequiresCode);
        Assert.Null(service.Check("ricardo", Password, null));
        Assert.Null(service.Check("ricardo", Password, "123456"));
        Assert.NotNull(service.Check("ricardo", Password, "005924"));
        Assert.Null(service.Check("ricardo", Password, "005924"));   // replayed

        // The next step's code still works.
        _time.Now = _time.Now.AddSeconds(30);
        var next = Totp.Code(Totp.Base32Decode(Secret), Totp.StepOf(_time.Now));
        Assert.NotNull(service.Check("ricardo", Password, next));
    }

    [Fact]
    public void A_right_code_with_a_wrong_password_does_not_sign_in()
    {
        var service = Service(new AuthUser { Username = "ricardo", PasswordHash = Hash, TotpSecret = Secret });

        Assert.Null(service.Check("ricardo", "wrong", "005924"));
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("/tax?year=2025", "/tax?year=2025")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("tax", "/")]
    public void Only_local_paths_are_returned_to(string? returnUrl, string expected) =>
        Assert.Equal(expected, SignInService.SafeReturnUrl(returnUrl));

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

public class AuthOptionsTests
{
    private static AuthUser User(string name = "ricardo", string hash = "hash", string? secret = null) =>
        new() { Username = name, PasswordHash = hash, TotpSecret = secret };

    [Fact]
    public void Without_users_the_app_runs_only_in_development()
    {
        Assert.Null(AuthSetup.Validate(new AuthOptions(), isDevelopment: true));
        Assert.Contains("Auth:Users is empty", AuthSetup.Validate(new AuthOptions(), isDevelopment: false));
        Assert.Null(AuthSetup.Validate(new AuthOptions { Users = [User()] }, isDevelopment: false));
    }

    [Fact]
    public void Incomplete_duplicate_or_unreadable_users_are_refused()
    {
        Assert.NotNull(AuthSetup.Validate(new AuthOptions { Users = [User(hash: "")] }, false));
        Assert.NotNull(AuthSetup.Validate(new AuthOptions { Users = [User(name: " ")] }, false));
        Assert.Contains("more than once", AuthSetup.Validate(new AuthOptions { Users = [User("a"), User("A")] }, false));
        Assert.Contains("not Base32", AuthSetup.Validate(new AuthOptions { Users = [User(secret: "not-base32!")] }, false));
        Assert.Contains("too short", AuthSetup.Validate(new AuthOptions { Users = [User(secret: "MZXW6YTB")] }, false));
        Assert.Null(AuthSetup.Validate(new AuthOptions { Users = [User(secret: Totp.NewSecret())] }, false));
    }
}

public class AuthCommandsTests
{
    [Theory]
    [InlineData(new[] { "new-feature-command" }, true)]
    [InlineData(new[] { "hash-passwrd" }, true)]
    [InlineData(new[] { "hash-password" }, false)]
    [InlineData(new[] { "--urls", "http://localhost:5000" }, false)]
    [InlineData(new[] { "/environment", "Development" }, false)]
    [InlineData(new[] { "Sync:Enabled=false" }, false)]
    [InlineData(new string[0], false)]
    public void A_plain_word_that_is_no_command_is_unknown_but_settings_are_not(string[] args, bool unknown) =>
        Assert.Equal(unknown, AuthCommands.IsUnknownCommand(args));

    [Fact]
    public void An_unknown_command_says_which_ones_exist_and_fails()
    {
        var error = new StringWriter();

        Assert.Equal(2, AuthCommands.Unknown(["new-feature-command"], error));
        Assert.Contains("Unknown command 'new-feature-command'", error.ToString());
        Assert.Contains("new-vapid-keys", error.ToString());
    }

    private static (int Exit, string Output, string Error) Run(string input, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exit = AuthCommands.Run(args, new StringReader(input), output, error);
        return (exit, output.ToString().Trim(), error.ToString());
    }

    [Fact]
    public void Hash_password_prints_a_hash_that_verifies()
    {
        var (exit, hash, _) = Run("a long password\na long password\n", "hash-password");

        Assert.Equal(0, exit);
        var service = new SignInService(Options.Create(new AuthOptions { Users = [new() { Username = "me", PasswordHash = hash }] }), TimeProvider.System);
        Assert.NotNull(service.Check("me", "a long password", null));
    }

    [Fact]
    public void Hash_password_refuses_short_or_mistyped_passwords()
    {
        Assert.Equal(1, Run("short\nshort\n", "hash-password").Exit);
        Assert.Contains("differ", Run("a long password\nanother one!!\n", "hash-password").Error);
    }

    [Fact]
    public void New_totp_secret_prints_the_secret_and_the_link()
    {
        var (exit, secret, error) = Run("", "new-totp-secret", "ricardo");

        Assert.Equal(0, exit);
        Assert.Equal(32, secret.Length);
        Assert.Contains($"otpauth://totp/T212Ext:ricardo?secret={secret}", error);
        Assert.True(AuthCommands.IsCommand(["new-totp-secret"]));
        Assert.False(AuthCommands.IsCommand(["--urls", "http://localhost"]));
    }
}
