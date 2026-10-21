using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.Globalization;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace Kynakee.MailAuthorization;

internal static class Program
{
    private static readonly ResourceManager Resources = new("Kynakee.MailAuthorization.Messages", typeof(Program).Assembly);
    private static readonly CompositeFormat DeviceInstructions = CompositeFormat.Parse(Message("DeviceInstructions"));

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            Console.WriteLine(Message("Help"));
            return 0;
        }
        if (args is not ["authorize"] and not ["verify-smtp"])
        {
            await Console.Error.WriteLineAsync(Message("InvalidArguments")).ConfigureAwait(false);
            return 2;
        }
        if (args is ["authorize"] && (Console.IsOutputRedirected || Console.IsInputRedirected))
        {
            await Console.Error.WriteLineAsync(Message("InteractiveTerminalRequired")).ConfigureAwait(false);
            return 2;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            timeout.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var session = new MailOAuthSession(configuration);
            if (args is ["verify-smtp"])
            {
                var username = configuration["Smtp:OAuth:Username"];
                var host = configuration["Smtp:Host"];
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(host) ||
                    !int.TryParse(configuration["Smtp:Port"], out var port) || port != 587)
                    throw new InvalidOperationException();
                var recipient = configuration["Smtp:VerificationRecipient"] ?? username;
                if (!System.Net.Mail.MailAddress.TryCreate(recipient, out var address) ||
                    !string.Equals(address.Address, recipient, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException();
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                var token = await session.GetAccessTokenAsync(timeout.Token).ConfigureAwait(false);
                using var message = new MimeKit.MimeMessage();
                message.From.Add(MimeKit.MailboxAddress.Parse(username));
                message.To.Add(MimeKit.MailboxAddress.Parse(address.Address));
                message.Subject = Message("SmtpTestSubject");
                message.Body = new MimeKit.TextPart("plain") { Text = Message("SmtpTestBody") };
                using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 60_000 };
                await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls, timeout.Token).ConfigureAwait(false);
                await client.AuthenticateAsync(new MailKit.Security.SaslMechanismOAuth2(username, token), timeout.Token).ConfigureAwait(false);
                await client.SendAsync(message, timeout.Token).ConfigureAwait(false);
                Console.WriteLine(Message("SmtpAccepted"));
                return 0;
            }
            Console.WriteLine(Message("AuthorizationStarting"));
            await session.AuthorizeAsync(code =>
            {
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, DeviceInstructions, code.VerificationUrl, code.UserCode));
                return Task.CompletedTask;
            }, timeout.Token).ConfigureAwait(false);
            Console.WriteLine(Message("AuthorizationSucceeded"));
            return 0;
        }
        catch (MailKit.CommandException)
        {
            await Console.Error.WriteLineAsync(Message("SmtpFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (MailKit.ProtocolException)
        {
            await Console.Error.WriteLineAsync(Message("SmtpFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (MailKit.Security.AuthenticationException)
        {
            await Console.Error.WriteLineAsync(Message("SmtpFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (System.Security.Authentication.AuthenticationException)
        {
            await Console.Error.WriteLineAsync(Message("SmtpFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (System.Net.Sockets.SocketException)
        {
            await Console.Error.WriteLineAsync(Message("SmtpFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync(Message("AuthorizationCancelled")).ConfigureAwait(false);
            return 130;
        }
        catch (InvalidOperationException)
        {
            await Console.Error.WriteLineAsync(Message("ConfigurationOrAccountInvalid")).ConfigureAwait(false);
            return 1;
        }
        catch (MsalException)
        {
            await Console.Error.WriteLineAsync(Message("AuthorizationFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (IOException)
        {
            await Console.Error.WriteLineAsync(Message("CacheAccessFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync(Message("CacheAccessFailed")).ConfigureAwait(false);
            return 1;
        }
        catch (CryptographicException)
        {
            await Console.Error.WriteLineAsync(Message("CacheAccessFailed")).ConfigureAwait(false);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static string Message(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
