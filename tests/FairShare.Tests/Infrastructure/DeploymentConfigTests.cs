using FairShare.Infrastructure.Persistence;
using FairShare.Infrastructure.Services;
using FluentAssertions;
using Npgsql;

namespace FairShare.Tests.Infrastructure;

/// <summary>Configuration helpers that hosted deployments depend on.</summary>
public class DeploymentConfigTests
{
    [Fact]
    public void Normalize_KeyValueString_IsReturnedUnchanged()
    {
        const string value = "Host=localhost;Port=5432;Database=fairshare_dev;Username=postgres;Password=postgres";

        PostgresConnectionString.Normalize(value).Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_Empty_IsReturnedUnchanged(string? value)
    {
        PostgresConnectionString.Normalize(value).Should().Be(value);
    }

    [Fact]
    public void Normalize_NeonStyleUrl_IsConvertedToKeyValueForm()
    {
        const string url =
            "postgresql://app_user:s3cr%40t@ep-cool-name-123.ap-southeast-1.aws.neon.tech/fairshare?sslmode=require&channel_binding=require";

        var builder = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize(url));

        builder.Host.Should().Be("ep-cool-name-123.ap-southeast-1.aws.neon.tech");
        builder.Port.Should().Be(5432);
        builder.Database.Should().Be("fairshare");
        builder.Username.Should().Be("app_user");
        builder.Password.Should().Be("s3cr@t");
        builder.SslMode.Should().Be(SslMode.Require);
    }

    [Fact]
    public void Normalize_PostgresSchemeWithPortAndVerifyFull_IsParsed()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Normalize("postgres://u:p@db.example.com:6543/app?sslmode=verify-full"));

        builder.Port.Should().Be(6543);
        builder.SslMode.Should().Be(SslMode.VerifyFull);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("REPLACE_WITH_A_STRONG_SECRET_KEY_MIN_32_CHARS")]
    public void RequireSigningKey_MissingWeakOrPlaceholder_Throws(string? key)
    {
        var act = () => JwtSigningKey.Require(key);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt__Key*");
    }

    [Fact]
    public void RequireSigningKey_RandomKey_IsAccepted()
    {
        const string key = "k3J9vQ2mX7pL4sT8wZ1nB6cF0hR5yU3e";

        JwtSigningKey.Require(key).Should().Be(key);
    }
}
